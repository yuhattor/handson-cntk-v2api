using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Swashbuckle.Swagger.Annotations;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using CNTK;
using Newtonsoft.Json.Linq;

namespace CNTKAzureTutorial01.Controllers
{
    public class ValuesController : ApiController
    {
        // GET api/values
        [SwaggerOperation("Post")]
        public async Task<string> Post()
        {
            string json = Request.Content.ReadAsStringAsync().Result;
            JObject requestBody = JObject.Parse(json);
            if (requestBody["url"] != null)
            {
                return await this.EvaluateCustomDNN((string)requestBody["url"]);
            }
            else
            {
                return "Please post target image url";
            }
        }

        public async Task<string> EvaluateCustomDNN(string imageUrl)
        //public static void EvaluationSingleImage(DeviceDescriptor device)
        {
            try
            {
                string domainBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string workingDirectory = Environment.CurrentDirectory;
                DeviceDescriptor device = DeviceDescriptor.CPUDevice;

                // Place your model in the folder
                string modelFilePath = Path.Combine(domainBaseDirectory, @"CNTK\Models\TransferLearning.model");
                Function modelFunc = Function.Load(modelFilePath, device);

                // Get input variable. The model has only one single input.
                // The same way described above for output variable can be used here to get input variable by name.
                Variable inputVar = modelFunc.Arguments.Single();
                // Get shape data for the input variable
                NDShape inputShape = inputVar.Shape;
                int imageWidth = inputShape[0];
                int imageHeight = inputShape[1];
                int imageChannels = inputShape[2];
                int imageSize = inputShape.TotalSize;

                // The model has only one output.
                // If the model have more than one output, use the following way to get output variable by name.
                // Variable outputVar = modelFunc.Outputs.Where(variable => string.Equals(variable.Name, outputName)).Single();
                Variable outputVar = modelFunc.Output;
                var inputDataMap = new Dictionary<Variable, Value>();
                var outputDataMap = new Dictionary<Variable, Value>();

                // Image preprocessing to match input requirements of the model.
                // This program uses images from the CIFAR-10 dataset for evaluation.
                // Please see README.md in <CNTK>/Examples/Image/DataSets/CIFAR-10 about how to download the CIFAR-10 dataset.
                // Transform the image
                System.Net.Http.HttpClient httpClient = new HttpClient();
                Stream imageStream = await httpClient.GetStreamAsync(imageUrl);
                Bitmap bmp = new Bitmap(Bitmap.FromStream(imageStream));
                var resized = bmp.Resize((int)imageWidth, (int)imageHeight, true);
                List<float> resizedCHW = resized.ParallelExtractCHW();

                // Create input data map
                var inputVal = Value.CreateBatch(inputVar.Shape, resizedCHW, device);
                inputDataMap.Add(inputVar, inputVal);

                // Create ouput data map. Using null as Value to indicate using system allocated memory.
                // Alternatively, create a Value object and add it to the data map.
                outputDataMap.Add(outputVar, null);

                // Start evaluation on the device
                modelFunc.Evaluate(inputDataMap, outputDataMap, device);

                // Get evaluate result as dense output
                var outputVal = outputDataMap[outputVar];
                var outputData = outputVal.GetDenseData<float>(outputVar);
                string json = JsonConvert.SerializeObject(outputData);
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}\nCallStack: {1}\n Inner Exception: {2}", ex.Message, ex.StackTrace, ex.InnerException != null ? ex.InnerException.Message : "No Inner Exception");
                throw ex;
            }
        }
    }
}
