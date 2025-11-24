using System.Text;

namespace Aiursoft.FileLock;

public static class PasswordHelper
{
    public static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            // intercept: true 表示拦截按键，不显示在控制台
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    // 可以在这里处理退格的视觉效果，如果需要显示 * 号的话
                    // Console.Write("\b \b"); 
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                // 如果想显示星号，取消下面注释
                // Console.Write("*");
            }
        }
        return password.ToString();
    }
}