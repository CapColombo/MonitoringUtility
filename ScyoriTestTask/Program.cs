using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json;

namespace ScyoriTestTask;

class Program
{
    const string APPSETTINGS = "appsettings.json"; // файл конфигурации
    const string CONNECTION_RESULTS = "connection_results.json"; // файл с результатами
    const string EMAIL_LOGIN = "***@gmail.com"; // email отправителя
    const string EMAIL_PASSWORD = "***"; // пароль отправителя
    const string SMTP_SERVER = "smtp.gmail.com";
    const int SMTP_PORT = 587;

    static async Task Main(string[] args)
    {
        // чтобы получить результаты последней проверки, в аргументы необходимо ввести "check"
        if (args.Length != 0 && args.Contains("check"))
        {
            try
            {
                Console.WriteLine("Считывание последних данных...");
                string path = Environment.CurrentDirectory;
                string json = File.ReadAllLines($"{CONNECTION_RESULTS}").Last(); // @$"bin\Debug\net6.0\{CONNECTION_RESULTS}"
                ResultsModel? data = JsonConvert.DeserializeObject<ResultsModel>(json);

                Console.WriteLine($"Дата: {data.Time}");
                Console.WriteLine($"{data.SiteResponse}");
                Console.WriteLine($"{data.DbResponse}");
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Данные не найдены");
                Console.ReadKey();
            }
        }

        else if (args.Length != 0)
        { 
            Console.WriteLine("Неизвестный аргумент");
            Console.ReadKey();
        }

        // если аргументов нет, то выполняется проверка соединений
        else
        {
            DataModel? data;

            // считывание строк из JSON
            try
            {
                string json = File.ReadAllText(APPSETTINGS);
                data = JsonConvert.DeserializeObject<DataModel>(json);
            }
            catch (Exception)
            {
                Console.WriteLine("Не удалось найти файл с настройками!");
                Console.ReadKey();
                return;
            }
            
            // проверка соединения с сайтом и БД
            bool isSiteResponds = await CheckWebsiteConnection(data.WebsiteConnectionString);
            bool isDBResponds = CheckDatabaseConnection(data.DatabaseConnectionString);

            SaveToFile(isSiteResponds, isDBResponds);
            SendToEmail(data.EmailAdresses, CONNECTION_RESULTS);
            Console.ReadKey();
        }
    }

    static void SaveToFile(bool isSiteResponds, bool isDBResponds)
    {
        using StreamWriter sw = new(CONNECTION_RESULTS, true);

        string db = isDBResponds ? "Соединение к базе данных прошло успешно" : "Не удалось подключиться к базе данных";
        string site = isSiteResponds ? "Ответ от сайта получен" : "Не удалось получить ответ от сайта";
        var time = DateTime.Now; // текущее время

        ResultsModel results = new() { DbResponse = db, SiteResponse = site, Time = time };
        string json = JsonConvert.SerializeObject(results);
        sw.WriteLine(json);
        sw.Close();

        Console.WriteLine("Запись в файл произведена");
    }

    static void SendToEmail(string[] adresses, string filePath)
    {
        if (adresses == null)
        {
            Console.WriteLine("Не указан адрес электронной почты");
            return;
        }

        MailAddress from = new(EMAIL_LOGIN, "User"); // отправитель

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

                SmtpClient smtp = new(SMTP_SERVER, SMTP_PORT); // адрес smtp-сервера и порт
                smtp.Credentials = new NetworkCredential(EMAIL_LOGIN, EMAIL_PASSWORD); // логин и пароль
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
                Console.WriteLine("Ответ от сайта получен");
                return true;
            }
            else
            {
                Console.WriteLine("Не удалось получить ответ от сайта");
                return false;
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Неправильный адрес");
            return false;
        }
    }

    static bool CheckDatabaseConnection(string connectionString)
    {
        SqlConnection connection = new(connectionString);
        try
        {
            connection.Open();
            Console.WriteLine("Успешное соединение к базе данных");
            return true;
        }
        catch (SqlException)
        {
            Console.WriteLine("Не удалось подключиться к базе данных");
            return false; 
        }
        finally
        {
            connection.Close();
        }
    }
}
