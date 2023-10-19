internal class Program {
    private static void horizontalLine(char c) {
        for(int i = 0; i < Console.WindowWidth; i++) Console.Write(c); Console.WriteLine();
    }

    private static string splitIntoLines(string toSplit, string indentor) {
        string result = indentor;
        string[] words = toSplit.Split(' ');
        int remainingChars = Console.WindowWidth - indentor.Length;

        foreach(string word in words) {
            if(remainingChars - word.Length - 1 >= 0) {
                result += word + " ";
                remainingChars -= word.Length + 1;
            }

            else if(remainingChars - word.Length - 1 < 0) {
                result += $"\n{indentor}{word} ";
                remainingChars = Console.WindowWidth - indentor.Length - word.Length;
            }
        }
        return result;
    }

    private static async Task<ekClient> pierakstisanas() {
        Console.Clear();
        ekClient lietotajs = new ekClient(host: "https://my.e-klase.lv");

        // Pierakstīšanās
        bool pierakstijas = false;
        do {
            // Iegūst lietotājvārdu un paroli
            Console.Write("Lietotājvārds: ");
            string? userName = Console.ReadLine();
            if(userName == null) userName = "";

            Console.Write("Parole: ");
            string? password = Console.ReadLine();
            if(password == null) password = "";

            // Mēģina pierakstīties
            bool izdevasPierakstities = await lietotajs.initialize(userName: userName, password: password);

            if(!izdevasPierakstities) {
                Console.Clear();
                Console.WriteLine("Nepareizs lietotājvārds un/vai parole!");
            }
            else pierakstijas = true;
        } while (!pierakstijas);

        return lietotajs;
    }

    private static int getChoice(int[] validChoices) {
        while(true) {
            string? input = Console.ReadLine();
            if(int.TryParse(input, out int intInput)) {
                if(validChoices.Contains(intInput)) {
                    return intInput;
                }
            }
            Console.Write(new string(' ', Console.WindowWidth));
        }
    }

    private static async Task Main(string[] args) {
        ekClient lietotajs = await pierakstisanas();
        string header = $"{lietotajs.name}\n{lietotajs.school}\n";

        /*
        -2 – beigt programmu
        -1 – izrakstīties
        0 – nav izvēle, galvenā izvēle
        1 – apskatīt dienasgrāmatu
        */
        int choice = 0;
        while (choice != -2){
            Console.Clear();


            Console.WriteLine($"{lietotajs.name}\n{lietotajs.school}");
            horizontalLine('═');

            if(choice == 0) {
                
            }

            switch (choice) {
                case 0:
                    Console.WriteLine(
                        "Ko tu vēlies darīt?\n-2 – Aizvērt šo programmu\n-1 – Izrakstīties\n1 – Apskatīt dienasgrāmatu"
                    );
                    choice = getChoice(new int[]{-2, -1, 1});
                    break;
                case -1:
                    lietotajs = await pierakstisanas();
                    choice = 0;
                    break;
                case 1:
                    int nedela = 420;
                    while(nedela == 420) {
                        Console.WriteLine("Kuras nedēļas dienasgrāmatu gribi apskatīties?\n-1 – iepriekšējā nedēļa\n0 – Šī nedēļā\n1 – nākamā nedēļa");
                        int.TryParse(Console.ReadLine(), out nedela);
                    }

                    var schedule = await lietotajs.Schedule(DateTime.Today.AddDays(nedela * 7));

                    Console.Clear();
                    Console.WriteLine(header);
                    horizontalLine('═');

                    Console.WriteLine($"{new string(' ', Console.WindowWidth / 2 - 6)}Dienasgrāmata");

                    foreach(KeyValuePair<string, dynamic> diena in schedule) {
                        Dictionary<string, dynamic> dienaV = diena.Value;
                        
                        string dienaStr = diena.Key;
                        string datums = dienaV["datums"];

                        // Uzraksta piemēram
                        // Pirmdiena ━━━━━━━━━━━━━━━━━━━━━━━━━━━ 16.10.23.
                        int between = Console.WindowWidth - dienaStr.Length - datums.Length - 2;
                        Console.WriteLine($"{dienaStr} {new string('━', between)} {datums}");

                        foreach(KeyValuePair<string, dynamic> stunda in dienaV["stundas"]) {
                            Dictionary<string, dynamic> stundaV = stunda.Value;

                            // stunda p.k., priekšmeta nosaukums, atzīmes
                            string prieksmets = $"{(stunda.Key[0] == '○' ? "○ " : stunda.Key)} {stundaV["prieksmets"]}";
                            string atzimes = string.Join(" | ", stundaV["atzimes"]);

                            between = Console.WindowWidth - prieksmets.Length - atzimes.Length;
                            Console.WriteLine($"{prieksmets}{new string(' ', between)}{atzimes}");
                        }
                    }

                    Console.Write("nospied jebkuru taustiņu, lai turpinātu");
                    Console.ReadKey();
                    choice = 0;
                    break;
            }
        };

        Console.Clear();
    }
}
