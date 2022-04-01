using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json;

namespace ScyoriTestTask;

class Program
{
    const string APPSETTINGS = "appsettings.json"; // файл конфигурации

    static async Task Main(string[] args)
    {
        DataModel? data;

        // считывание настроек из JSON
        try
        {
            string json = File.ReadAllText(APPSETTINGS);
            data = JsonConvert.DeserializeObject<DataModel>(json);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Не удалось найти файл с настройками!");
            Console.ReadKey();
            return;
        }

        // чтобы получить результаты последней проверки, в аргументы необходимо ввести "check"
        if (args.Length != 0 && args.Contains("check"))
        {
            GetLastResults(data.ConnectionResultsPath);
            Console.ReadKey();
            return;
        }
        else if (args.Length != 0)
        { 
            Console.WriteLine("Неизвестный аргумент");
            Console.ReadKey();
            return;
        }
        
        // проверка соединения с сайтом и БД
        bool isSiteResponds = await CheckWebsiteConnection(data.WebsiteConnectionString);
        CheckDatabaseConnection(data.DatabaseConnectionString, out Dictionary<string, string> dbResults);

        SaveToFile(data.ConnectionResultsPath, data.WebsiteConnectionString, isSiteResponds, dbResults);
        SendToEmail(data.EmailAdresses, data.EmailLogin, data.EmailPassword,
            data.ConnectionResultsPath, data.SmtpServer, data.SmtpPort);
        Console.ReadKey();
    }

    static void GetLastResults(string path)
    {
        try
        {
            Console.WriteLine("Считывание последних данных...");
            string json = File.ReadAllLines($"{path}").Last();
            ResultsModel? result = JsonConvert.DeserializeObject<ResultsModel>(json);

            Console.WriteLine($"Дата: {result.Time}");
            Console.WriteLine($"{result.SiteResponse}");
            Console.WriteLine($"{result.DbResponse}");
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("Данные не найдены");
        }
    }

    static void SaveToFile(string path, string uri, bool isSiteResponds, Dictionary<string, string> dbResults)
    {
        using StreamWriter sw = new(path, true);

        string db = bool.Parse(dbResults["connection"]) ?
            $"Соединение к базе данных {dbResults["dbName"]} на сервере {dbResults["dbServer"]} прошло успешно" :
                $"Не удалось подключиться к базе данных {dbResults["dbName"]} на сервере {dbResults["dbServer"]}";
        string site = isSiteResponds ? $"Ответ от сайта {uri} получен" : $"Не удалось получить ответ от сайта {uri}";
        var time = DateTime.Now; // текущее время

        ResultsModel results = new() { DbResponse = db, SiteResponse = site, Time = time };
        string json = JsonConvert.SerializeObject(results);
        sw.WriteLine(json);
        sw.Close();

        Console.WriteLine("Запись в файл произведена");
    }

    static void SendToEmail(string[] adresses, string login, string password,
        string filePath, string smtpServer, int smtpPort)
    {
        if (string.IsNullOrEmpty(adresses[0]))
        {
            Console.WriteLine("Не указан адрес электронной почты");
            return;
        }

        MailAddress from = new(login, "User"); // отправитель

        foreach (var adress in adresses)
        {
            try
            {
                MailAddress to = new(adress); // получатель
                MailMessage m = new(from, to);
                m.IsBodyHtml = true; // письмо представляет код html
                m.Subject = "Результаты"; // тема письма
                m.Attachments.Add(new Attachment(filePath)); // прикрепляем файл
                m.Body = "<h2>Письмо с результатами</h2>"; // текст письма

                SmtpClient smtp = new(smtpServer, smtpPort); // адрес smtp-сервера и порт
                smtp.Credentials = new NetworkCredential(login, password); // логин и пароль
                smtp.EnableSsl = true;
                smtp.Timeout = 10000; // ожидание 10 секунд
                smtp.Send(m);

                Console.WriteLine($"{adress} получил файл");
            }
            catch (Exception)
            {
                Console.WriteLine($"Не удалось отправить файл на адрес {adress}");
                continue;
            }
        }
    }

    static async Task<bool> CheckWebsiteConnection(string uri)
    {
        try
        {
            HttpClient client = new();
            var result = await client.GetAsync(uri);

            if (result != null)
            {
                Console.WriteLine($"Ответ от сайта {uri} получен");
                return true;
            }
            else
            {
                Console.WriteLine($"Не удалось получить ответ от сайта {uri}");
                return false;
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Неправильный адрес");
            return false;
        }
    }

    static void CheckDatabaseConnection(string connectionString, out Dictionary<string, string> dbResults)
    {
        string[] connectionParams = connectionString.Split(';'); // параметры подключения
        int index = connectionParams[0].IndexOf('='); // индекс знака =
        int index2 = connectionParams[1].IndexOf('='); // индекс знака =

        dbResults = new();
        dbResults["dbServer"] = connectionParams[0].Substring(index+1); // сервер БД 
        dbResults["dbName"] = connectionParams[1].Substring(index2+1); // имя БД

        SqlConnection connection = new(connectionString);
        try
        {
            connection.Open();
            Console.WriteLine($"Успешное соединение к базе данных {dbResults["dbName"]} на сервере {dbResults["dbServer"]}");
            dbResults["connection"] = "true";
            return;
        }
        catch (SqlException)
        {
            Console.WriteLine($"Не удалось подключиться к базе данных {dbResults["dbName"]} на сервере {dbResults["dbServer"]}");
            dbResults["connection"] = "false";
            return; 
        }
        finally
        {
            connection.Close();
        }
    }
}
