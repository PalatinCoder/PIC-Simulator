namespace PIC_Simulator.Utility
{
    class BindableByte
    {
        public byte Value { get; set; }

        public override string ToString()
        {
            return string.Format("{0:X2}", Value);
        }

        public static implicit operator byte(BindableByte bb)
        {
            return bb.Value;
        }
        public static implicit operator BindableByte(byte b)
        {
            BindableByte bb = new BindableByte() { Value = b };
            return bb;
        }
    }
}
