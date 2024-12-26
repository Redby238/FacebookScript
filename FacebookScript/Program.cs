using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace FacebookProfileScraper
{
    class Program
    {
        private static readonly string TwoCaptchaApiKey = "8331ed21072f8ba168582668fc83db1e"; // У вас цей API-ключ може не працювати(Сервіс платний :) )
   //(Пункт який може допомогти з уникненням капчі)//Headless-браузер - використання режиму headless мінімізує ризик виявлення ботів виконуючих капчу
   //[] - Початок роботи скрипта
 //[] - Перехід на сторінку входу Facebook
  //[ ] - CAPTCHA виявлена.Спроба обійти через 2Captcha...
  //[] - Помилка: Timed out after 20 seconds
 //[] - Завершення роботи скрипта 
  //такий порядок лога має бути при корректній роботі скрипта(обійти капчу бесплатними сервісами не зміг(


        [Obsolete]
        static async Task Main(string[] args)
        {
           

            string logFile = "out.log";
            Console.WriteLine($"Логи будуть збережені у: {Path.GetFullPath(logFile)}");
            Thread.Sleep(1000);
            using (StreamWriter log = new StreamWriter(logFile, true))
            {
                var chromeOptions = new ChromeOptions();

                chromeOptions.AddArgument("--disable-software-rasterizer"); // Вимкнення програмного рендерингу
                chromeOptions.AddArgument("--no-sandbox"); // Вимкнення sandbox
                chromeOptions.AddArgument("--disable-dev-shm-usage"); // Уникнення обмежень на спільну пам'ять
                chromeOptions.AddArgument("--disable-blink-features=AutomationControlled"); // Приховування автоматизації
                chromeOptions.AddArgument("--headless"); // Запуск у безголовному режимі

                log.WriteLine($"[{DateTime.Now}] - Початок роботи скрипта");
                IWebDriver driver = new ChromeDriver(chromeOptions);
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

                try
                {
                    
                    driver.Navigate().GoToUrl("https://www.facebook.com/login");
                    log.WriteLine($"[{DateTime.Now}] - Перехід на сторінку входу Facebook");

                    
                    wait.Until(d => d.FindElement(By.Id("email"))).SendKeys("popovicustinian@gmail.com");
                    driver.FindElement(By.Id("pass")).SendKeys("yourpassword123123");
                    driver.FindElement(By.Name("login")).Click();

                    
                    if (driver.PageSource.Contains("captcha"))
                    {
                        log.WriteLine($"[{DateTime.Now}] - CAPTCHA виявлена. Спроба обійти через 2Captcha...");

                        string siteKey = ExtractReCaptchaSiteKey(driver.PageSource);
                        string pageUrl = driver.Url;

                        string captchaToken = await SolveReCaptcha(siteKey, pageUrl);
                        if (!string.IsNullOrEmpty(captchaToken))
                        {
                            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                            js.ExecuteScript($"document.querySelector('[name=\"g-recaptcha-response\"]').innerHTML='{captchaToken}';");
                            js.ExecuteScript("document.querySelector('[name=\"captcha_form\"]').submit();");
                        }
                    }

                   
                    wait.Until(d => d.FindElement(By.CssSelector("a[title='Profile']"))).Click();
                    var profilePic = wait.Until(d => d.FindElement(By.CssSelector("img[alt='Profile picture']")));
                    string profilePicUrl = profilePic.GetAttribute("src");

                    log.WriteLine($"[{DateTime.Now}] - URL аватарки: {profilePicUrl}");
                    Console.WriteLine($"Profile Picture URL: {profilePicUrl}");
                }
                catch (Exception ex)
                {
                    log.WriteLine($"[{DateTime.Now}] - Помилка: {ex.Message}");
                }
                finally
                {
                    driver.Quit();
                    log.WriteLine($"[{DateTime.Now}] - Завершення роботи скрипта");
                }
            }
        }

        static string ExtractReCaptchaSiteKey(string pageSource)
        {
           
            var start = pageSource.IndexOf("data-sitekey=\"") + "data-sitekey=\"".Length;
            var end = pageSource.IndexOf("\"", start);
            return pageSource.Substring(start, end - start);
        }

        static async Task<string> SolveReCaptcha(string siteKey, string pageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                
                var response = await client.GetStringAsync(
                    $"http://2captcha.com/in.php?key={TwoCaptchaApiKey}&method=userrecaptcha&googlekey={siteKey}&pageurl={pageUrl}&json=1");

                var responseJson = JObject.Parse(response);
                if (responseJson["status"].ToString() != "1")
                {
                    Console.WriteLine("Помилка при створенні CAPTCHA завдання");
                    return null;
                }

                string requestId = responseJson["request"].ToString();

               
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(5000);
                    var resultResponse = await client.GetStringAsync(
                        $"http://2captcha.com/res.php?key={TwoCaptchaApiKey}&action=get&id={requestId}&json=1");

                    var resultJson = JObject.Parse(resultResponse);
                    if (resultJson["status"].ToString() == "1")
                    {
                        return resultJson["request"].ToString();
                    }
                }

                Console.WriteLine("Не вдалося отримати відповідь від 2Captcha");
                return null;
            }
        }
    }
}
