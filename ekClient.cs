using System.Text.RegularExpressions;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;

class ekClient {
    private HttpClient webClient = new HttpClient();
    public string name = "";
    public string school = "";
    
    private async Task<IDocument> angleHtml(string source) {
        IConfiguration config = Configuration.Default;
        IBrowsingContext context = BrowsingContext.New(config);
        return await context.OpenAsync(req => req.Content(source));
    }

    // Iegūst vārdu, uzvārdu un klasi, skolu
    private async Task getDetails() {
        // Iegūst mājaslapu
        var response = await webClient.GetAsync("/Family/Home?login=1");
        response.EnsureSuccessStatusCode();

        // izveidot Angle# dokumentu no tās lapas
        string responseS = await response.Content.ReadAsStringAsync();
        IDocument doc = await angleHtml(responseS);

        // Paņem vārdu, uzvārdu un skolu no tās lapas
        name = doc.GetElementsByClassName("name")[0].InnerHtml;
        school = doc.GetElementsByClassName("school")[0].InnerHtml;
    }

    private async Task<bool> logIn(string userName, string password) {
        // Pievieno lietotāja ievadīto lietotājvārdu un paroli payload
        var payload = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("UserName", userName),
            new KeyValuePair<string, string>("Password", password)
        });

        // Aizsūta login pieprasījumu
        var response = await webClient.PostAsync("/?v=15", payload);
        response.EnsureSuccessStatusCode();
        
        // Pārbauda vai izdevās ielogoties, pārbaudot, vai dokumentā ir teikts, ka ir ir nepareizi ievadīts lietotājvārds/parole
        string responseS = await response.Content.ReadAsStringAsync();
        return !responseS.Contains("Nepareizi ievadīts lietotājvārds un/vai parole.");
    }

    // Noņem html tagus no stringa
    private string removeHtmlTags(string text) {
        string result = "";
        bool element = false;
        foreach(char a in text) {
            if(a == '<') element = true;
            else if(a == '>') element = false;
            else if(!element) result += a;
        }
        result = result.Trim();
        return result;
    }

    // Atrod linkus dotajā elementā(div span utt.)
    private string[,] getLinks(IElement element) {
        var linkElements = element.GetElementsByTagName("a"); // Visi link(a) elementi
        string[,] saites = new string[linkElements.Length, 2]; // Gala array piemērs: {{"Mājasdarbs", "https://cornhub.com"}, {"https://app.soma.lv/viedtema/geografija/astota-klase/kas-ir-dabas-resu...", "https://app.soma.lv/viedtema/geografija/astota-klase/kas-ir-dabas-resursi/turisma-resursi"}}

        for(int i = 0; i < linkElements.Length; i++) {
            string saite = linkElements[i].GetAttribute("href"); 
            if(saite.StartsWith("/Attachment/Get/") || saite.StartsWith("/Auth/OAuth/"))
                saite = "https://my.e-klase.lv" + saite; // Pabeidz e-klases pielikumu saites, jo tās eklasē parādas bez sākuma

            saites[i, 0] = removeHtmlTags(linkElements[i].InnerHtml); // Linka teksts

            if(saite.Contains("destination_uri=")) {
                saite = saite.Substring(saite.IndexOf("destination_uri=") + 16); // 16 = "destination_uri=" garums
                saite = HttpUtility.UrlDecode(saite);
            }
            saites[i, 1] = saite; // Links
        };

        return saites;
    }

    // Metode, kas atgriež visas nedēļas stundu sarakstu, kā nested dictionary
    public async Task<Dictionary<string, dynamic>> Schedule(DateTime week/*jebkuras nedēļas dienas datums*/) { // datuma formāts: diena.mēnesis.pilnsGads
        Dictionary<string, dynamic> schedule = new Dictionary<string,dynamic>();
        string diena = "";
        Regex repeatingWhiteSpaces = new Regex(" +");

        /* /schedule/ formāts { 
            "Pirmdiena": {
                "datums": "17.10.23",
                "stundas" : {
                    "1" : {
                        "prieksmets" : "Matemātika",
                        "klase" : "214",
                        "tema" : "Veselu skaitļu saskaitīšana",
                        "tema_saites" : {
                            "Saite 0" : "https://google.com"
                        }
                        "uzdots" : "Mācību grāmata 5 lpp. 1.uzd",
                        "uzdots_saites" : {
                            "Saite 0" : "https://google.com"
                        }
                        "atzimes" : ["74.56%", "Ns"] // kaut kas šāds ar mēdz gadīties
                        }
                    }...
                },
                "uzvedibasIeraksti" : ["Neorganizēts, neklause norādēm: Stundas laikā nepārstaj kāpt uz galda(negatīvs)(22.09., Klases stunda, Jānis Bērziņš)"];
            }...
        */

        // Dabū dienasgrāmatas lapu
        string link = "/Family/Diary?Date=" + string.Format("{0:dd.MM.yyyy}", week); // Pievieno datumu, tekošajai nedēļai datumu nevajag
        var response = await webClient.GetAsync(link);
        response.EnsureSuccessStatusCode();

        // Izveido Angle# Dokumentu no diensgramatas html
        var doc = await angleHtml(await response.Content.ReadAsStringAsync());

        // Dienasgrāmatas lapas elements, kurā ir dienas + tabulas
        var scheduleR = doc.GetElementsByClassName("student-journal-lessons-table-holder hidden-xs")[0];

        // Dienas
        for(int i = 0; i < scheduleR.ChildElementCount; i++) {
            string[] uzvedibasIeraksti = {};
            // Tabula
            if(i % 2 != 0) {
                // Apaļo( ○ ) stundu skaitītājs šajā dienā
                int apalasStundas = 0;

                // Iet cauri tabulas rindām(stundām, uzvedības ierakstiem)
                var scheduleR1 = scheduleR.Children[i];
                for(int j = 0; j < scheduleR1.Children[1].ChildElementCount; j++) {
                    var scheduleR2 = scheduleR.Children[i].Children[1].Children[j];

                    // Pārliecinās, vai šī nav uzvedības ierakstu rinda
                    if(scheduleR2.GetAttribute("class") != "info") {

                        // Iet cauri priekšmeta nosaukumiem | tēmām | uzdotajiem | atzīmēm
                        string stundasNr = "";
                        for(int u = 0; u < scheduleR2.ChildElementCount; u++) {
                        
                            var currentElement = scheduleR2.Children[u];
                            switch (u){

                                case 0: // Priekšmets, Klase, Stunda p.k.
                                    string prieksmets = currentElement
                                        .GetElementsByClassName("title")[0]
                                        .InnerHtml
                                        .Trim()
                                        .Replace("\n", "");
                                    prieksmets = prieksmets
                                        .Substring(0, prieksmets.IndexOf("  "));
                                    
                                    string klase = currentElement
                                        .GetElementsByClassName("title")[0]
                                        .GetElementsByClassName("room")[0]
                                        .InnerHtml;

                                    stundasNr = currentElement
                                        .GetElementsByClassName("number")[0]
                                        .GetElementsByClassName("number--lessonNotInDay")
                                        .Length > 0 
                                        ?
                                        "○" + (++apalasStundas).ToString()
                                        :
                                        currentElement
                                            .GetElementsByClassName("number")[0]
                                            .InnerHtml
                                            .Trim();
                                        
                                    schedule[diena]["stundas"].Add(stundasNr, new Dictionary<string, dynamic>());
                                    schedule[diena]["stundas"][stundasNr].Add("prieksmets", prieksmets);
                                    schedule[diena]["stundas"][stundasNr].Add("klase", klase);

                                    break;

                                case 1: // Tēma
                                    var saites = getLinks(currentElement);

                                    string tema = removeHtmlTags(currentElement.InnerHtml);
                                    tema = tema.Replace("\n", "").Trim();

                                    schedule[diena]["stundas"][stundasNr].Add("tema_saites", new Dictionary<string, string>());
                                    // Aizvieto linkus ar Saite1, Saite2...
                                    for(int v = 0; v < saites.Length / 2; v++) {
                                        if(saites.Length == 0) break;

                                        if(saites[v, 0].StartsWith("https://") || saites[v, 0].StartsWith("http://"))
                                            tema = tema.Replace(saites[v, 0], $" (Saite {v + 1}) ");
                                        else
                                            tema = tema.Replace(saites[v, 0], $"{saites[v, 0]}(Saite {v + 1})");
                                
                                        schedule[diena]["stundas"][stundasNr]["tema_saites"]
                                            .Add($"Saite {v + 1}", saites[v, 1]);
                                    }
                                    
                                    // Aizvieto " ", kas atkārtojas ar vienu " "
                                    
                                    tema = repeatingWhiteSpaces.Replace(tema, " ");

                                    schedule[diena]["stundas"][stundasNr].Add("tema", tema);
                                    break;

                                // Uzdots
                                case 2:
                                    saites = getLinks(currentElement);

                                    string uzdots = removeHtmlTags(currentElement.InnerHtml);
                                    uzdots = uzdots.Replace("\n", "").Trim();

                                    schedule[diena]["stundas"][stundasNr].Add("uzdots_saites", new Dictionary<string, string>());
                                    // Aizvieto linkus ar Saite1, Saite2...
                                    for(int v = 0; v < saites.Length / 2; v++) {
                                        if(saites.Length == 0) break;

                                        if(saites[v, 0].StartsWith("https://") || saites[v, 0].StartsWith("http://") || saites[v, 0] == "Atvērt")
                                            uzdots = uzdots.Replace(saites[v, 0], $" (Saite {v + 1}) ");
                                        else
                                            uzdots = uzdots.Replace(saites[v, 0], $"{saites[v, 0]}(Saite {v + 1})");
                                
                                        schedule[diena]["stundas"][stundasNr]["uzdots_saites"]
                                            .Add($"Saite {v + 1}", saites[v, 1]);
                                    }

                                    uzdots = repeatingWhiteSpaces.Replace(uzdots, " ");

                                    schedule[diena]["stundas"][stundasNr].Add("uzdots", uzdots);
                                    break;

                                // Atzīme/Apmeklējums
                                case 3:
                                    // Console.WriteLine("Atzīmes/Apmeklējums:");
                                    string[] atzimes = {};
                                    for(int n = 0; n < currentElement.ChildElementCount; n++) {
                                        string atzime = removeHtmlTags(currentElement.Children[n].InnerHtml);
                                        atzimes = atzimes.Append(atzime).ToArray();
                                    }
                                    schedule[diena]["stundas"][stundasNr].Add("atzimes", atzimes);
                                    break;
                            }   
                        }
                    }
                
                    // Uzvedības ieraksti
                    if(scheduleR2.GetAttribute("class") == "info") {
                        string uzvedibasIeraksts = scheduleR2.GetElementsByClassName("info-content")[0].InnerHtml;
                        uzvedibasIeraksts = removeHtmlTags(uzvedibasIeraksts);
                        uzvedibasIeraksti.Append(uzvedibasIeraksts);
                    }
                    
                }
                schedule[diena].Add("uzvedibasIeraksti", uzvedibasIeraksti);
            }
            

            else { // Datums, Diena, Klase
                var currentElement = scheduleR.Children[i];

                diena = currentElement.InnerHtml.Trim();
                diena = diena.Substring(diena.IndexOf(" ") + 1);
                diena = diena.Substring(0, diena.IndexOf(" "));
                diena = char.ToUpper(diena[0]) + diena.Substring(1); // Pirmais burts - lielais

                string datums = currentElement.InnerHtml.Trim();
                datums = datums.Substring(0, datums.IndexOf(" "));

                // pievieno datumu "schedule" vārdnīcai
                schedule.Add(diena, new Dictionary<string, dynamic>());
                schedule[diena].Add("datums", datums);
                schedule[diena].Add("stundas", new Dictionary<string, dynamic>());
            }
        }
        return schedule;
    }

    // Konstruktora async papildinājums
    public async Task<bool> initialize(string userName, string password) {
        if(await logIn(userName, password) == false)
            return false;
        await getDetails(); 

        return true;
    }

    // Konstruktors
    public ekClient(string host) {
        webClient.BaseAddress = new Uri(host);
        webClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36");
    }
}
