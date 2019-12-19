using MoneyKeeper;
using MoneyKeeper.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace MoneyKeeper
{
    public static class InMemoryDb
    {
        public static Dictionary<string, Guid> ActiveSessions =
            new Dictionary<string, Guid>();

        public static ICollection<ApplicationUser> Users = new List<ApplicationUser>()
        {
            new ApplicationUser() { Email = "iskander@gmail.com", Password = "12345", Id = Guid.NewGuid() ,Balance=80}
        };
    }
    public static class Router
    {
        public static Dictionary<string, IHandler> Routes = new Dictionary<string, IHandler>()
        {
            { "/app/register", new RegistrationHandler() },
            { "/app/sign-in", new SignInHandler() },
            //{ "/app/",}
        };
    }
    public class BadRequestResponseContainer
    {
        public string Message { get; set; }
    }


    public interface ICommand { }
    public class RegisterUserCommand : ICommand
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
    }
    public class SignInUserCommand : ICommand
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class ChangePasswordCommand
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }

    public interface IHandler
    {
        void Handle(HttpListenerRequest requestInfo, HttpListenerResponse responseInfo);
    }

    #region
    public class RegistrationHandler : IHandler
    {
        public void Handle(HttpListenerRequest requestStream, HttpListenerResponse responseStream)
        {
            using (var ms = new MemoryStream())
            {
                requestStream.InputStream.CopyTo(ms);
                var requestBody = ms.ToArray();
                var requestString = Encoding.UTF8.GetString(requestBody);

                var command = JsonConvert.DeserializeObject<RegisterUserCommand>(requestString);

                var userExists = InMemoryDb.Users
                    .Any(p => p.Email.Equals(command.Email, StringComparison.InvariantCultureIgnoreCase));

                if (userExists)
                {
                    var errorMessage = new BadRequestResponseContainer() { Message = "Пользователь уже существует!" };
                    responseStream.StatusCode = 400;

                    var json = JsonConvert.SerializeObject(errorMessage);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    responseStream.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    responseStream.StatusCode = 201;
                }
            }
        }
    }


    public class SessionInterceptor
    {
        public Guid? GetCurrentUserBySessionId(HttpListenerRequest requestInfo, HttpListenerResponse responseInfo)
        {
            var headerValue = requestInfo.Headers["Authorization"];
            if (headerValue == null)
            {
                string message = "NOT AUTHORIZED!";
                var bytes = Encoding.UTF8.GetBytes(message);
                responseInfo.StatusCode = 401;
                responseInfo.OutputStream.Write(bytes);
                responseInfo.Close();

                return null;
            }
            else
            {
                var sessionExists = InMemoryDb.ActiveSessions.ContainsKey(headerValue);
                if (sessionExists)
                {
                    return InMemoryDb.ActiveSessions[headerValue];
                }
                else
                {
                    string message = "NOT AUTHORIZED!";
                    var bytes = Encoding.UTF8.GetBytes(message);
                    responseInfo.StatusCode = 401;
                    responseInfo.OutputStream.Write(bytes);
                    responseInfo.Close();

                    return null;
                }
            }
        }
    }


    public class SignInHandler : IHandler
    {
        public void Handle(HttpListenerRequest requestInfo, HttpListenerResponse responseInfo)
        {
            using (var ms = new MemoryStream())
            {
                requestInfo.InputStream.CopyTo(ms);
                var requestBody = ms.ToArray();
                var requestString = Encoding.UTF8.GetString(requestBody);

                var command = JsonConvert.DeserializeObject<SignInUserCommand>(requestString);

                var user = InMemoryDb.Users
                    .FirstOrDefault(p => p.Email == command.Email &&
                        p.Password == command.Password);

                if (user != null)
                {
                    var sessionId = string.Format("{0}-{1}", Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                        .Replace("-", string.Empty);

                    InMemoryDb.ActiveSessions[sessionId] = user.Id;

                    var okMessage = new { SessionId = sessionId };
                    responseInfo.StatusCode = 200;

                    var json = JsonConvert.SerializeObject(okMessage);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    responseInfo.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    var errorMessage = "Неправильно введен логин или пароль";
                    responseInfo.StatusCode = 400;
                    var json = JsonConvert.SerializeObject(errorMessage);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    responseInfo.OutputStream.Write(bytes, 0, bytes.Length);
                }
            }
        }
    }

}
public class ChangePasswordHandler : IHandler
{
    public void Handle(HttpListenerRequest requestInfo, HttpListenerResponse responseInfo)
    {
        var sessionInterceptor = new SessionInterceptor();
        var userId = sessionInterceptor.GetCurrentUserBySessionId(requestInfo, responseInfo);
        if (userId != null)
        {
            using (var ms = new MemoryStream())
            {
                requestInfo.InputStream.CopyTo(ms);
                var requestBody = ms.ToArray();
                var requestString = Encoding.UTF8.GetString(requestBody);

                var command = JsonConvert.DeserializeObject<ChangePasswordCommand>(requestString);

                var userPasswordChange = InMemoryDb.Users.FirstOrDefault(w => w.Email == "iskander@gmail.com") as ApplicationUser;
                userPasswordChange.Password = command.NewPassword;
                responseInfo.StatusCode = 200;
            }
        }
        else
        {
            responseInfo.StatusCode = 401;
            var errorMessage = "Unauthorized!";
            var json = JsonConvert.SerializeObject(errorMessage);
            var bytes = Encoding.UTF8.GetBytes(json);

            responseInfo.OutputStream.Write(bytes);
        }
    }
}
public class GetAvailableBalance : IHandler
{
    public void Handle(HttpListenerRequest requestInfo, HttpListenerResponse responseInfo)
    {
        var sessionInterceptor = new SessionInterceptor();
        var userId = sessionInterceptor.GetCurrentUserBySessionId(requestInfo, responseInfo);
        if (userId != null)
        {
            var user = InMemoryDb.Users.First(w => w.Id == userId);
            responseInfo.StatusCode = 200;
            var balance = new
            {
                balance = user.Balance
            };
            var json = JsonConvert.SerializeObject(balance);
            var bytes = Encoding.UTF8.GetBytes(json);

            responseInfo.OutputStream.Write(bytes);

        }
        else
        {
            responseInfo.StatusCode = 401;
            responseInfo.StatusCode = 401;
            var errorMessage = "Unauthorized!";
            var json = JsonConvert.SerializeObject(errorMessage);
            var bytes = Encoding.UTF8.GetBytes(json);

            responseInfo.OutputStream.Write(bytes);
        }
    }
}
#endregion




class Program
{
    static string _bindingEndpoint = "http://localhost:15100/app/";
    static bool _serverOnActiveState = true;
    static void Main(string[] args)
    {
        //using (var httpClient = new HttpClient())
        //{
        //    var uri = "http://localhost:15100/app/change-password";
        //    httpClient.PostAsync(uri,;
        //    //httpClient.PostAsync();
        //}

        var serverListener = new HttpListener();
        serverListener.Prefixes.Add(_bindingEndpoint);

        serverListener.Start();
        Console.WriteLine("Server is running!");

        while (_serverOnActiveState)
        {
            Console.WriteLine("Waiting for the next request!");

            var next = serverListener.GetContext();
            var requestInfo = next.Request;
            var responseInfo = next.Response;

            if (requestInfo.RawUrl == "/app/echo")
            {
                var responsePage = File.ReadAllText(@"c:\data\welcome-page.html");
                var echoContent = Encoding.UTF8.GetBytes(responsePage);
                responseInfo.AddHeader("Content-Type", "text/html");
                responseInfo.OutputStream.Write(echoContent, 0, echoContent.Length);
                responseInfo.Close();

                continue;
            }
            else if (requestInfo.RawUrl == "/app/get-me")
            {
                var sessionInterceptor = new SessionInterceptor();
                var userId = sessionInterceptor.GetCurrentUserBySessionId(requestInfo, responseInfo);

            }
            else if (requestInfo.RawUrl == "/app/change-password")
            {
                var handler = Router.Routes[requestInfo.RawUrl];
                responseInfo.AddHeader("Content-Type", "application/json");
                handler.Handle(requestInfo, responseInfo);

                responseInfo.Close();
            }
            else
            {
                var handler = Router.Routes[requestInfo.RawUrl];

                responseInfo.AddHeader("Content-Type", "application/json");
                handler.Handle(requestInfo, responseInfo);

                responseInfo.Close();
            }
        }

        Console.WriteLine("Completed request processing!");
    }
}

