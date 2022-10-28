using Miniscript;

namespace ThingsEditor.Scripting
{
    public class ValUniqueSymbol : Value
    {
        public readonly string Label;

        public ValUniqueSymbol(string label)
        {
            Label = label;
        }

        public override string ToString(TAC.Machine vm)
        {
            return $"UniqueSymbol[{Label}]";
        }

        public override int Hash(int recursionDepth = 16)
        {
            return Label.GetHashCode();
        }

        public override double Equality(Value rhs, int recursionDepth = 16)
        {
            return ReferenceEquals(rhs, this) ? 1 : 0;
        }
    }
}