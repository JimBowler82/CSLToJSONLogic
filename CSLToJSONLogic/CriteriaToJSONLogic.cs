using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace CSLToJSONLogic
{
    public class CriteriaToJSONLogic
    {
        private static readonly Dictionary<string, string> OpMap = new Dictionary<string, string>
    {
        { "=", "==" },
        { "<>", "!=" },
        { ">", ">" },
        { "<", "<" },
        { ">=", ">=" },
        { "<=", "<=" }
    };

        public static string ConvertToJSONLogic(string criteria)
        {
            criteria = criteria.Trim('"');
            var tokens = Tokenize(criteria);
            var jsonLogic = ParseExpression(tokens);
            var res = jsonLogic.ToString((Newtonsoft.Json.Formatting)Formatting.Indented);
            return res;
        }

        private static List<string> Tokenize(string criteria)
        {
            var tokens = Regex.Split(criteria, @"(\band\b|\bor\b|\bnot\b|\(|\)|\s+|isnullorempty\(\[.*?\]\)|contains\(\[.*?\],\s*['""]?.*?['""]?\)|startswith\(\[.*?\],\s*['""]?.*?['""]?\)|endswith\(\[.*?\],\s*['""]?.*?['""]?\)|isnull\(\[.*?\]\)|isnotnull\(\[.*?\]\))", RegexOptions.IgnoreCase);
            var result = new List<string>();
            foreach (var token in tokens)
            {
                var trimmedToken = token.Trim();
                if (!string.IsNullOrEmpty(trimmedToken))
                    result.Add(trimmedToken);
            }
            return result;
        }



        private static JToken ParseExpression(List<string> tokens)
        {
            var stack = new Stack<JToken>();
            var operatorStack = new Stack<string>();
            int i = 0;

            while (i < tokens.Count)
            {
                string token = tokens[i].ToLower();
                if (token == "(")
                {
                    operatorStack.Push(token);
                }
                else if (token == ")")
                {
                    while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
                    {
                        ProcessOperator(stack, operatorStack.Pop());
                    }
                    operatorStack.Pop(); // Pop the "("
                }
                else if (token == "and" || token == "or")
                {
                    while (operatorStack.Count > 0 && Precedence(operatorStack.Peek()) >= Precedence(token))
                    {
                        ProcessOperator(stack, operatorStack.Pop());
                    }
                    operatorStack.Push(token);
                }
                else if (token == "not")
                {
                    operatorStack.Push(token);
                }
                else
                {
                    JToken operand = ParseOperand(tokens, ref i);
                    stack.Push(operand);
                }
                i++;
            }

            while (operatorStack.Count > 0)
            {
                ProcessOperator(stack, operatorStack.Pop());
            }

            return stack.Pop();
        }



        private static void ProcessOperator(Stack<JToken> stack, string op)
        {
            if (op == "not")
            {
                JToken operand = stack.Pop();
                stack.Push(new JObject(new JProperty("!", operand)));
            }
            else
            {
                JToken right = stack.Pop();
                JToken left = stack.Pop();
                stack.Push(new JObject(new JProperty(op, new JArray { left, right })));
            }
        }

        private static int Precedence(string op)
        {
            if (op == "or") return 1;
            if (op == "and") return 2;
            if (op == "not") return 3;
            return 0;
        }

        private static JToken ParseOperand(List<string> tokens, ref int i)
        {
            string token = tokens[i];
            string lowerToken = token.ToLower();

            if (i + 2 < tokens.Count && OpMap.ContainsKey(tokens[i + 1]))
            {
                string field = token.Trim('[', ']');
                string op = OpMap[tokens[i + 1]];
                string value = tokens[i + 2].Trim('#', '\'', '"');
                i += 2;
                return new JObject(new JProperty(op, new JArray { new JObject(new JProperty("var", field)), value }));
            }
            if (token.StartsWith("#") && token.EndsWith("#"))
            {
                string dateValue = token.Trim('#');
                return dateValue;
            }
            if (lowerToken.StartsWith("contains("))
            {
                var match = Regex.Match(token, @"contains\(\[(.*?)\],\s*['""](.*?)['""]\)", RegexOptions.IgnoreCase);
                string field = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                return new JObject(new JProperty("in", new JArray { value, new JObject(new JProperty("var", field)) }));
            }
            if (lowerToken.StartsWith("startswith("))
            {
                var match = Regex.Match(token, @"startswith\(\[(.*?)\],\s*['""](.*?)['""]\)", RegexOptions.IgnoreCase);
                string field = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                return new JObject(new JProperty("startsWith", new JArray { new JObject(new JProperty("var", field)), value }));
            }
            if (lowerToken.StartsWith("endswith("))
            {
                var match = Regex.Match(token, @"endswith\(\[(.*?)\],\s*['""](.*?)['""]\)", RegexOptions.IgnoreCase);
                string field = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                return new JObject(new JProperty("endsWith", new JArray { new JObject(new JProperty("var", field)), value }));
            }
            if (lowerToken.StartsWith("isnull("))
            {
                var match = Regex.Match(token, @"isnull\(\[(.*?)\]\)", RegexOptions.IgnoreCase);
                string field = match.Groups[1].Value;
                return new JObject(new JProperty("==", new JArray { new JObject(new JProperty("var", field)), JValue.CreateNull() }));
            }
            if (lowerToken.StartsWith("isnotnull("))
            {
                var match = Regex.Match(token, @"isnotnull\(\[(.*?)\]\)", RegexOptions.IgnoreCase);
                string field = match.Groups[1].Value;
                return new JObject(new JProperty("!=", new JArray { new JObject(new JProperty("var", field)), JValue.CreateNull() }));
            }
            if (lowerToken.StartsWith("isnullorempty("))
            {
                var match = Regex.Match(token, @"isnullorempty\(\[(.*?)\]\)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string field = match.Groups[1].Value;
                    return new JObject(new JProperty("or", new JArray {
                new JObject(new JProperty("==", new JArray { new JObject(new JProperty("var", field)), JValue.CreateNull() })),
                new JObject(new JProperty("==", new JArray { new JObject(new JProperty("var", field)), "" }))
            }));
                }
            }
            if (Regex.IsMatch(token, @"^\[.*\]$", RegexOptions.IgnoreCase))
            {
                string field = token.Trim('[', ']');
                return new JObject(new JProperty("var", field));
            }
            throw new InvalidOperationException($"Unrecognized token: {token}");
        }




    }
}
