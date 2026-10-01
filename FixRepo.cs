using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        string path = "backend/Api/Master/Master.Repository/Repositories/RestaurantSettingsRepository.cs";
        string text = File.ReadAllText(path);
        
        var regex = new Regex(@"public class RestaurantSettingsView.*?^\}", RegexOptions.Singleline | RegexOptions.Multiline);
        text = regex.Replace(text, "");
        
        File.WriteAllText(path, text);
    }
}
