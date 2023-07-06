namespace Halibut.Tests.Support
{
    public class Reference<T>
    {
        public Reference()
        {
        }

        public Reference(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }
}
