using Miniscript;

namespace ThingsEditor.Scripting
{
    public class ValWrapper : Value
    {
        public readonly object content;

        public ValWrapper(object content)
        {
            this.content = content;
            if (content is ValWrapperNotificationReceiver) ((ValWrapperNotificationReceiver)content).WrapperAdded(this);
        }

        ~ValWrapper()
        {
            if (content is ValWrapperNotificationReceiver)
                ((ValWrapperNotificationReceiver)content).WrapperRemoved(this);
        }

        public override string ToString(TAC.Machine vm)
        {
            return content.ToString().Replace("UnityEngine.", "");
        }

        public override int Hash(int recursionDepth = 16)
        {
            return content.GetHashCode();
        }

        public override double Equality(Value rhs, int recursionDepth = 16)
        {
            return rhs is ValWrapper wrapper && wrapper.content == content ? 1 : 0;
        }
    }

    public interface ValWrapperNotificationReceiver
    {
        void WrapperAdded(ValWrapper wrapper);
        void WrapperRemoved(ValWrapper wrapper);
    }
}