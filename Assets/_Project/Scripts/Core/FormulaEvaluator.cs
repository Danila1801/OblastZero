// Assets/_Project/Scripts/Core/FormulaEvaluator.cs
using System;
using System.Globalization;

namespace OblastZero.Core
{
    /// <summary>Thrown for any parse error, unknown variable, or divide-by-zero in a formula.</summary>
    public class FormulaException : Exception
    {
        public FormulaException(string message) : base(message) { }
    }

    /// <summary>
    /// A tiny, allocation-light arithmetic evaluator for event success-chance formulas
    /// (design bible §6.2, e.g. <c>"0.3 + 0.4 * (crew.charisma / 100)"</c>). Supports + - * /, unary minus,
    /// parentheses, decimal literals (invariant culture), and dotted variable names resolved through a
    /// caller-supplied delegate. On any parse error, unknown variable, or divide-by-zero it throws
    /// <see cref="FormulaException"/> so callers can fall back to a static chance instead of crashing.
    ///
    /// Deliberately NOT a scripting language: no functions, comparisons, or side effects. Grammar:
    ///   expr    := term (('+' | '-') term)*
    ///   term    := unary (('*' | '/') unary)*
    ///   unary   := '-' unary | primary
    ///   primary := number | variable | '(' expr ')'
    /// </summary>
    public sealed class FormulaEvaluator
    {
        /// <summary>Resolves a variable name to a value. Return false for unknown names (evaluator will throw).</summary>
        public delegate bool VariableResolver(string name, out double value);

        private readonly string _src;
        private readonly VariableResolver _resolver;
        private int _pos;

        private FormulaEvaluator(string src, VariableResolver resolver)
        {
            _src = src;
            _resolver = resolver;
        }

        /// <summary>Evaluates <paramref name="formula"/>, resolving variables via <paramref name="resolver"/>.</summary>
        public static double Evaluate(string formula, VariableResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(formula))
                throw new FormulaException("Empty formula.");

            var ev = new FormulaEvaluator(formula, resolver);
            double result = ev.ParseExpr();
            ev.SkipWhitespace();
            if (ev._pos != ev._src.Length)
                throw new FormulaException($"Unexpected trailing input at position {ev._pos} in '{formula}'.");
            return result;
        }

        private double ParseExpr()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            double value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                {
                    value *= ParseUnary();
                }
                else if (Match('/'))
                {
                    double divisor = ParseUnary();
                    if (Math.Abs(divisor) < double.Epsilon)
                        throw new FormulaException($"Divide by zero in '{_src}'.");
                    value /= divisor;
                }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (Match('-')) return -ParseUnary();
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (_pos >= _src.Length)
                throw new FormulaException($"Unexpected end of formula '{_src}'.");

            char c = _src[_pos];
            if (c == '(')
            {
                _pos++; // consume '('
                double inner = ParseExpr();
                SkipWhitespace();
                if (!Match(')')) throw new FormulaException($"Missing ')' in '{_src}'.");
                return inner;
            }

            if (char.IsDigit(c) || c == '.') return ParseNumber();
            if (char.IsLetter(c) || c == '_') return ParseVariable();

            throw new FormulaException($"Unexpected character '{c}' at position {_pos} in '{_src}'.");
        }

        private double ParseNumber()
        {
            int start = _pos;
            while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.')) _pos++;
            string token = _src.Substring(start, _pos - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormulaException($"Invalid number '{token}' in '{_src}'.");
            return value;
        }

        private double ParseVariable()
        {
            int start = _pos;
            while (_pos < _src.Length &&
                   (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_' || _src[_pos] == '.')) _pos++;
            string name = _src.Substring(start, _pos - start);
            if (_resolver == null || !_resolver(name, out double value))
                throw new FormulaException($"Unknown variable '{name}' in '{_src}'.");
            return value;
        }

        private void SkipWhitespace()
        {
            while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos])) _pos++;
        }

        private bool Match(char c)
        {
            if (_pos < _src.Length && _src[_pos] == c) { _pos++; return true; }
            return false;
        }
    }
}
