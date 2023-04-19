using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests;

public class HalibutProxyFixture
{
    [Test]
    public void ThrowsSpecificErrorWhenErrorTypeIsSet()
    {
        ServerError serverError = ResponseMessage.ServerErrorFromException(new MethodNotFoundHalibutClientException("not found", "not even mum could find it"));
        
        var errorThrower = () => HalibutProxy.ThrowExceptionFromError(serverError);
        
        errorThrower.Should().Throw<MethodNotFoundHalibutClientException>();
    }
    
    [Test]
    public void ThrowsGenericErrorWhenErrorTypeIsUnknown()
    {
        ServerError serverError = new ServerError{ Message = "bob", Details = "details", ErrorType = "Foo.BarException" };
        
        var errorThrower = () => HalibutProxy.ThrowExceptionFromError(serverError);
        
        errorThrower.Should().Throw<HalibutClientException>();
    }
    
    [Test]
    public void ThrowsGenericErrorWhenErrorTypeIsNull_EGWhenTalkingToAnOlderHalibutVersion()
    {
        ServerError serverError = new ServerError{ Message = "bob", Details = "details", ErrorType = null };
        
        var errorThrower = () => HalibutProxy.ThrowExceptionFromError(serverError);
        
        errorThrower.Should().Throw<HalibutClientException>();
    }
}