namespace App.Services.EmailSender
{
    public class StmpEmailSenderOptions
    {
        public string Host {get;set;} = "smtp.qq.com";
        public string User { get; set; }
        public string Key { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
    }
}