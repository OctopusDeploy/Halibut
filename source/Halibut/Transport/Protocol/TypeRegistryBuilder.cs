using System.Reflection;

namespace Halibut.Transport.Protocol
{
    public class TypeRegistryBuilder
    {
        Assembly[]? typeAssemblies;

        /// <summary>
        /// Adds a list of assemblies to the <see cref="ITypeRegistry"/>. Used to search for contract types for derived types.
        /// </summary>
        /// <param name="assemblies">An array of <see cref="Assembly">Assemblies</see> to add to the <see cref="ITypeRegistry"/>.</param>
        public TypeRegistryBuilder WithTypeAssemblies(params Assembly[] assemblies)
        {
            typeAssemblies = assemblies;
            return this;
        }

        public ITypeRegistry Build()
        {
            return new TypeRegistry(typeAssemblies);
        }
    }
}