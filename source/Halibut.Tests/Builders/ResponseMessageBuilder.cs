using Halibut.Transport.Protocol;

namespace Halibut.Tests.Builders
{
    public class ResponseMessageBuilder
    {
        readonly string id;

        public ResponseMessageBuilder(string id)
        {
            this.id = id;
        }

        public static ResponseMessageBuilder FromRequest(RequestMessage requestMessage)
        {
            var responseMessageBuilder = new ResponseMessageBuilder(requestMessage.Id);
            return responseMessageBuilder;
        }

        public ResponseMessage Build()
        {
            var response = new ResponseMessage
            {
                Id = id,
                Result = "Hello World"
            };
            return response;
        }
    }
}