using System;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class HalibutProxyFixture
    {
        [Test]
        public void ThrowsSpecificErrorWhenErrorTypeIsSet()
        {
            ServerError serverError = ResponseMessage.ServerErrorFromException(new MethodNotFoundHalibutClientException("not found", "not even mum could find it"));
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<MethodNotFoundHalibutClientException>();
        }
    
        [Test]
        public void ThrowsGenericErrorWhenErrorTypeIsUnknown()
        {
            ServerError serverError = new ServerError{ Message = "bob", Details = "details", HalibutErrorType = "Foo.BarException" };
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<HalibutClientException>();
        }
    
        [Test]
        public void ThrowsGenericErrorWhenErrorTypeIsNull_ForExampleWhenTalkingToAnOlderHalibutVersion()
        {
            ServerError serverError = new ServerError{ Message = "bob", Details = "details", HalibutErrorType = null };
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<HalibutClientException>();
        }
        
        [Test]
        public void BackwardCompatibility_ServiceNotFound_IsThrownAsA_ServiceNotFoundHalibutClientException()
        {
            ServerError serverError = new ServerError{ 
                Message = "Service not found: IEchoService", 
                Details = @"System.Exception: Service not found: IEchoService
   at Halibut.ServiceModel.DelegateServiceFactory.GetService(String name) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/DelegateServiceFactory.cs:line 32
   at Halibut.ServiceModel.DelegateServiceFactory.CreateService(String serviceName) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/DelegateServiceFactory.cs:line 24
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 23
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request) in /home/auser/Documents/octopus/Halibut4/source/Halibut/HalibutRuntime.cs:line 256
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor) in /home/auser/Documents/octopus/Halibut4/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs:line 266",
                HalibutErrorType = null };
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<ServiceNotFoundHalibutClientException>();
        }
        
        [Test]
        public void BackwardsCompatibility_MethodNotFound_IsThrownAsA_MethodNotFoundHalibutClientException()
        {
            ServerError serverError = new ServerError{ 
                Message = "Service System.Object::SayHello not found", 
                Details = null,
                HalibutErrorType = null };
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<MethodNotFoundHalibutClientException>();
        }
        
        [Test]
        public void BackwardCompatibility_AmbiguousMethodMatch_IsThrownAsA_NoMatchingServiceOrMethodHalibutClientException()
        {
            ServerError serverError = new ServerError{ 
                Message = @"More than one possible match for the requested service method was found given the argument types. The matches were:
 - System.String Ambiguous(System.String, System.String)
 - System.String Ambiguous(System.String, System.Tuple`2[System.String,System.String])
The request arguments were:
String, <null>
", 
                Details = @"System.Reflection.AmbiguousMatchException: More than one possible match for the requested service method was found given the argument types. The matches were:
 - System.String Ambiguous(System.String, System.String)
 - System.String Ambiguous(System.String, System.Tuple`2[System.String,System.String])
The request arguments were:
String, <null>

   at Halibut.ServiceModel.ServiceInvoker.SelectMethod(IList`1 methods, RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 102
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 31
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request) in /home/auser/Documents/octopus/Halibut4/source/Halibut/HalibutRuntime.cs:line 256
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor) in /home/auser/Documents/octopus/Halibut4/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs:line 266",
                HalibutErrorType = null };
        
            Action errorThrower = () => HalibutProxy.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));
        
            errorThrower.Should().Throw<AmbiguousMethodMatchHalibutClientException>();
        }
    }
}