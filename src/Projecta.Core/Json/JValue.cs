using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projecta.Json
{
    public class JValue: JToken
    {
        public static JValue CreateUndefined()
        {
            return new JValue(JTokenType.Undefined, null);
        }

        public static JToken CreateNull()
        {
            return new JValue(JTokenType.Null, null);
        }

        internal readonly object? Value;


        private JValue(JTokenType type, object? value)
            : base(type)
        {
            if (value is string text) Projecta.Execution.EvaluationBudget.Current?.CheckString(text.Length);
            this.Value = value;
        }

        public JValue(double value) : this(JTokenType.Float, DoubleToDecimal(value)) {}
        public JValue(decimal value) : this(JTokenType.Float, value) { }
        public JValue(long value) : this(JTokenType.Integer, value) { }
        public JValue(int value) : this(JTokenType.Integer, value) { }
        public JValue(string value) : this(JTokenType.String, value) { }
        public JValue(char value) : this(JTokenType.String, value.ToString()) { }
        public JValue(bool value) : this(JTokenType.Boolean, value) { }
        public JValue(DateTimeOffset value) : this(JTokenType.String, value.ToString("O", CultureInfo.InvariantCulture)) { }   
        public JValue(DateTime value) : this(JTokenType.String, value.ToString("O", CultureInfo.InvariantCulture)) { }
        public JValue(TimeSpan value) : this(JTokenType.String, value.ToString("c", CultureInfo.InvariantCulture)) { }

        /// <summary>Returns an invariant, round-trippable representation of the stored number.</summary>
        public string GetNumberText() => this.Type switch
        {
            JTokenType.Integer => ((long)this).ToString(CultureInfo.InvariantCulture),
            JTokenType.Float when this.Value is decimal d => d.ToString("G29", CultureInfo.InvariantCulture),
            JTokenType.Float => ((double)this).ToString("R", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("The token is not a number.")
        };

        /// <summary>Returns the original decimal representation only when no double-to-decimal rounding is needed.</summary>
        public bool TryGetExactDecimal(out decimal value)
        {
            if (this.Value is decimal d) { value = d; return true; }
            if (this.Type == JTokenType.Integer) { value = (long)this; return true; }
            value = default; return false;
        }

        private static object DoubleToDecimal(double value)
        {
            if (Double.IsInfinity(value) || Double.IsNaN(value))
            {
                throw new JsonataException(JsonataErrorCode.S0102, "Number out of range: " + value);
            }
            try
            {
                decimal decimalValue = (decimal)value;
                if ((double)decimalValue == value)
                {
                    return decimalValue;
                }
                else
                {
                    return value;
                }
            }
            catch (Exception)
            {
                return value;
            }
        }

        private void ToString(StringBuilder builder)
        {
            switch (this.Type)
            {
            case JTokenType.Null:
                builder.Append("null");
                break;
            case JTokenType.Undefined:
                builder.Append("undefined");
                break;
            case JTokenType.Float:
                if (this.Value is decimal decimalValue)
                {
                    //see https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
                    //see https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#SpecifierExponent
                    //see https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number/toString
                    // Scientific notation is used if the radix is 10 and the number's magnitude (ignoring sign) is greater than or equal to 10^21 or less than 10^-6.
                    string format = "g";    //General
                    if (decimalValue != 0 && (Math.Abs(decimalValue) < 1e-6m || Math.Abs(decimalValue) >= 1e21m))
                    {
                        //format = "e";
                        format = "0.############################e+0";
                    }

                    builder.Append(decimalValue.ToString(format, CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(((double)this).ToString("g", CultureInfo.InvariantCulture));
                }
                break;
            case JTokenType.Integer:
                builder.Append(((long)this).ToString(CultureInfo.InvariantCulture));
                break;
            case JTokenType.String:
                builder.Append('"');
                JToken.EscapeString((string)this, builder);
                builder.Append('"');
                break;
            case JTokenType.Boolean:
                builder.Append((bool)this? "true" : "false");
                break;
            default:
                throw new Exception("Unexpected type " + this.Type);
            }
        }

        internal override void ToIndentedStringImpl(StringBuilder builder, int indent, SerializationSettings options)
        {
            this.ToString(builder);
        }

        internal override void ToStringFlatImpl(StringBuilder builder, SerializationSettings options)
        {
            this.ToString(builder);
        }

        public override JToken DeepClone()
        {
            return new JValue(this.Type, this.Value);
        }

        public override bool DeepEquals(JToken other)
        {
            if (this.Type != other.Type)
            {
                return false;
            }
            JValue otherValue = (JValue)other;
            if (this.Value == otherValue.Value)
            {
                return true;
            }

            switch (this.Type)
            {

            case JTokenType.Float:
                {
                    if (this.TryGetExactDecimal(out decimal left) && otherValue.TryGetExactDecimal(out decimal right))
                        return left == right;
                    return ((double)this).Equals((double)otherValue);
                }
            case JTokenType.Integer:
                {
                    long thisV = (long)this;
                    long otherV = (long)otherValue;
                    return thisV == otherV;
                }
            case JTokenType.String:
                {
                    string thisV = (string)this;
                    string otherV = (string)other;
                    return String.CompareOrdinal(thisV, otherV) == 0;
                }
            case JTokenType.Boolean:
                {
                    bool thisV = (bool)this;
                    bool otherV = (bool)otherValue;
                    return thisV == otherV;
                }
            case JTokenType.Null:
            case JTokenType.Undefined:
            default:
                throw new Exception("Unexpected type " + this.Type);
            }
        }
    }
}
