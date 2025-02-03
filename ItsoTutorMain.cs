using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
// Add Azure OpenAI package
using Azure.AI.OpenAI;
using Azure;
using Newtonsoft.Json;

namespace ITSOTutor
{
    public class ItsoTutorMain
    {
        private readonly ILogger<ItsoTutorMain> _logger;
        private readonly string? oaiEndpoint;
        private readonly string? oaiKey;
        //private static string oaiDeploymentName = "gpt-35-turbo-16k";
        private static string oaiDeploymentName = "gpt-4o-mini";
        private readonly string? azureSearchEndpoint;
        private readonly string? azureSearchKey;
        private readonly string? azureSearchIndex;
        private static OpenAIClient client;
        private static AzureSearchChatExtensionConfiguration ownDataConfig;
        private static string SystemMessage;


        public ItsoTutorMain(ILogger<ItsoTutorMain> logger)
        {
            _logger = logger;
            oaiEndpoint = Environment.GetEnvironmentVariable("AZURE_OIA_EP");
            oaiKey = Environment.GetEnvironmentVariable("AZURE_OIA_KEY");
            azureSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_EP");
            azureSearchKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_KEY");
            azureSearchIndex = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX");
            
            // Initialize the Azure OpenAI client
            client = new OpenAIClient(new Uri(oaiEndpoint), new AzureKeyCredential(oaiKey));

            // Configure your data source
            ownDataConfig = new()
            {
                SearchEndpoint = new Uri(azureSearchEndpoint),
                Authentication = new OnYourDataApiKeyAuthenticationOptions(azureSearchKey),
                IndexName = azureSearchIndex
            };

            // System message to provide context to the model
            SystemMessage =
                "You are an expert assistant in Interoperable Public Transport specifications aka ITSO. " +
                "Answer using only the provided context from RAG-retrieved documents, focusing on accuracy. " +
                "If information is missing, politely notify the user.";
        }

        [Function("ItsoTutorMain")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("..Sending the following request to Azure OpenAI endpoint...");

            // Read the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            
            // Process the message
            string userMessage = data?.message;
            _logger.LogInformation("Request: \" + userMessage + \"\\n\"");
            string responseMessage = ProcessUserMessage(userMessage);

            return new OkObjectResult(new { text = responseMessage });
        }

        private static string ProcessUserMessage(string userMessage)
        {
            // Implement basic logic here. For example:
            if (string.IsNullOrEmpty(userMessage))
            {
                return "I'm sorry, I didn't understand that. Could you please repeat?";
            }

            ChatCompletionsOptions chatCompletionsOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(SystemMessage),
                    new ChatRequestUserMessage(userMessage)
                },
                MaxTokens = 600,
                Temperature = 0.9f,
                DeploymentName = oaiDeploymentName,
                // Specify extension options
                AzureExtensionsOptions = new AzureChatExtensionsOptions()
                {
                    Extensions = { ownDataConfig }
                }
            };

            ChatCompletions response = client.GetChatCompletions(chatCompletionsOptions);
            ChatResponseMessage responseMessage = response.Choices[0].Message;

            // Basic echo functionality
            return responseMessage.Content;
        }
    }
}
