using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection.Metadata;
using TestTask.Models;

namespace SaveVacancyStats
{
    public class Handler
    {
        public async Task<Response> FunctionHandler(Request request/*, Context context*/)
        {
            string bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME");
            string objectKey = "vacancies_stats.json";

            Console.WriteLine("Поиск вакансий на hh.ru");
            try
            {
                // 1. Получение статистики HeadHunter
                int vacancyCount = await GetVacancyCountFromHH();

                // 2.Загрузка существующих данных(если есть)
                List<VacancyData> existingData = await LoadExistingData(bucketName, objectKey);

                // 3. Обновление или добавление новой записи
                DateTime today = DateTime.Now.Date;
                VacancyData newData = new VacancyData { Date = today.ToString("yyyy-MM-dd"), Vacancies = vacancyCount };

                int index = existingData.FindIndex(item => item.Date == newData.Date);
                if (index >= 0)
                {
                    existingData[index] = newData; // Замена существующей записи
                }
                else
                {
                    existingData.Add(newData); // Добавление новой записи
                }

                await SaveDataToS3(existingData, bucketName, objectKey);

                return new Response(400, $"Сохранено {vacancyCount} вакансий за {today.ToString("yyyy-MM-dd")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
                return new Response(500, ex.Message);
            }
        }

        private async Task<int> GetVacancyCountFromHH()
        {
            string query = Uri.EscapeDataString("C# Developer");
            string hhApiUrl = $"https://api.hh.ru/vacancies?text={query}&per_page=100";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "HandlerFunction/1.0");
            HttpResponseMessage response = await client.GetAsync(hhApiUrl);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            var jsonResponse = System.Text.Json.JsonDocument.Parse(responseBody);
            return jsonResponse.RootElement.GetProperty("found").GetInt32();
        }

        private async Task<List<VacancyData>> LoadExistingData(string bucketName, string objectKey)
        {
            AmazonS3Client s3Client = new AmazonS3Client(
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                new AmazonS3Config
                {
                    ServiceURL = Environment.GetEnvironmentVariable("YANDEX_S3_ENDPOINT")
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
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<VacancyData>>(contents) ?? new List<VacancyData>();
                }
            }
            catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Файл не существует, возвращаем пустой список
                return new List<VacancyData>();
            }
        }

        private async Task SaveDataToS3(List<VacancyData> data, string bucketName, string objectKey)
        {
            AmazonS3Client s3Client = new AmazonS3Client(
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
                Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
                new AmazonS3Config
                {
                    ServiceURL = Environment.GetEnvironmentVariable("YANDEX_S3_ENDPOINT")
                }
            );

            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                ContentBody = jsonContent
            };

            await s3Client.PutObjectAsync(request);
        }
    }
}