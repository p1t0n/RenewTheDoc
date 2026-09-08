# Renders `claude --output-format stream-json` into readable console lines.
# Kept deliberately forgiving: unknown event shapes are skipped rather than fatal,
# because a formatter crash must never take down an iteration.

def trunc($n): if (. | length) > $n then (.[0:$n] + " …") else . end;
def oneline: gsub("\\s+"; " ") | ltrimstr(" ") | rtrimstr(" ");

def tool_summary($name; $input):
  if $name == "Bash" then ($input.command // "" | oneline | trunc(120))
  elif $name == "Read" or $name == "Write" or $name == "Edit" then ($input.file_path // "" | trunc(100))
  elif ($name | startswith("mcp__linear-server")) then
    ([$input.id // $input.team // "", $input.title // ""] | map(select(. != "")) | join(" "))
  elif $name == "Grep" or $name == "Glob" then ($input.pattern // "" | trunc(80))
  elif $name == "TodoWrite" then "todo update"
  else ($input | tostring | oneline | trunc(100))
  end;

if .type == "system" and .subtype == "init" then
  "· session \(.session_id // "?" | .[0:8]) · model \(.model // "?")"

elif .type == "assistant" then
  ( .message.content // [] )
  | map(
      if .type == "text" then
        (.text // "" | oneline | select(length > 0) | trunc(600))
      elif .type == "tool_use" then
        "→ \(.name) \(tool_summary(.name; .input // {}))" | oneline | trunc(160)
      else empty
      end
    )
  | .[]

elif .type == "user" then
  ( .message.content // [] )
  | map(
      if .type == "tool_result" then
        ( if (.is_error // false) then "  ✗ " else "  ← " end ) +
        ( ( if (.content | type) == "string" then .content
            elif (.content | type) == "array" then ((.content | map(.text // "") | join(" ")))
            else "" end ) | oneline | trunc(160) )
      else empty
      end
    )
  | .[]

elif .type == "result" then
  ( "",
    "───── result ─────",
    (.result // "" | trunc(4000)),
    "· \(.num_turns // 0) turns · \(((.duration_ms // 0) / 1000) | floor)s · $\((.total_cost_usd // 0) * 10000 | floor | . / 10000)"
  )

else empty
end
