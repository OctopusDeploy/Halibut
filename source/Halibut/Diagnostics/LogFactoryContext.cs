using System;

namespace Halibut.Diagnostics
{
    public static class LogFactoryContext
    {
        static ILogFactory logFactory = new LogFactory();

        public static ILogFactory CurrentLogFactory
        {
            get { return logFactory; }
        }

        public static void OverrideLogFactory(ILogFactory factory)
        {
            logFactory = factory;
        }
    }
}