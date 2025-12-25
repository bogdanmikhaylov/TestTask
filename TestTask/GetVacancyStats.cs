using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection.Metadata;
using TestTask.Models;

namespace GetVacancyStats
{
    public class Handler
    {
        public async Task<Response> FunctionHandler(Request request)
        {
            string bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME");
            string objectKey = "vacancies_stats.json";

            try
            {
                // 1. Загрузка из S3
                List<VacancyData> data = await LoadDataFromS3(bucketName, objectKey);

                // 2. Сериализация в JSON
                string jsonResult = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                if (data.Count > 0)
                {
                    return new Response(200, jsonResult);
                }
                else
                {
                    return new Response(404, "Файл не найден");
                }
            }
            catch (Exception ex)
            {
                return new Response(500, ex.Message);
            }
        }

        private async Task<List<VacancyData>> LoadDataFromS3(string bucketName, string objectKey)
        {
            AmazonS3Client s3Client = new AmazonS3Client(
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                new AmazonS3Config
                {
                    ServiceURL = Environment.GetEnvironmentVariable("YANDEX_S3_ENDPOINT"),
                    ForcePathStyle = true
                }
            );
            
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                using (GetObjectResponse response = await s3Client.GetObjectAsync(request))
                using (System.IO.StreamReader reader = new System.IO.StreamReader(response.ResponseStream))
                {
                    string contents = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<List<VacancyData>>(contents) ?? new List<VacancyData>();
                }
            }
            catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("Ошибка при получении файла: " + e.Message);
                // Файл не существует, возвращаем пустой список
                return new List<VacancyData>();
            }
        }
    }
}