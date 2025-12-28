# KSeF Services - opis aplikacji
wersja 1.0.0

**KSeF.Services** to "działająca w tle"  aplikacja (*background service*) Microsoft Windows[^1]. Pozwala korzystającym z niej programom wywoływać metody API KSeF i zwraca rezultaty tych metod. Wykonuje wymagane przez to API zaawansowane operacje na danych wejściowych, związane z szyfrowaniem, liczeniem skrótów, czy infrastrukturą PKI. Korzystając z niej można implementować obsługę KSeF w  starszych językach programowania lub skryptach (np. JScript, VBA). W dalszym tekście będę je określał jako <u>**Klienta**</u>, a Ksef.Services - jako <u>**serwer**</u>.

**KSeF.Services.exe** jest programem Open Source, napisanym w .NET 9.0. Wykorzystuje oficjalną bibliotekę MF .NET dla KSeF. Z Klientem wymienia dane poprzez potoki nazwane. Zdecydowałem się na tę rzadziej spotykaną formę komunikacji, gdyż:
* implementacja takiego serwera lepiej pasuje do modelu serwisów i *Direct Injection*, sugerowanych w dokumentacji KSeF jako najlepszy sposób stosowania ich biblioteki;
* pomimo tego, że komunikacja przez potoki jest **wolniejsza** od komunikacji np. poprzez interfejsy COM, to jest jednak jest o rząd wielkości (lub dwa) szybsza od komunikacji HTTP z serwerem KSeF. Czyli w tym przypadku to spowolnienie jest zaniedbywalne; 
* potoki nazwane są dostępne dla każdego języka/środowiska  programowania, które jest w stanie otwierać lokalne pliki tekstowe;
* program serwera nie wymaga żadnej instalacji / rejestracji, poza wgraniem gdzieś folderu z jego binariami (plikiem **.exe* i towarzyszącymi mu **.dll*). To jest jedno ze specyficznych wymagań mojej firmy.

<a name="spis-tresci"></a>
## Spis treści
<!--TOC-->
  - [Wprowadzenie](#wprowadzenie)
  - [Podstawowa zasada komunikacji przez potoki nazwane](#podstawowa-zasada-komunikacji-przez-potoki-nazwane)
  - [Argumenty linii poleceń programu](#argumenty-linii-polecen-programu)
  - [Plik z konfiguracją programu](#plik-z-konfiguracja-programu)
  - [Implementacja klienta Ksef.Services](#implementacja-klienta-ksef.services)
  - [Informacje zwracane przez potok diagnostyczny](#informacje-zwracane-przez-potok-diagnostyczny)
  - [Dzienniki programu](#dzienniki-programu)
- [Lista poleceń Ksef.Services](#lista-polecen-ksef.services)
  - [Polecenia wspólne](#polecenia-wspolne)
    - [END](#end)
  - [Polecenia lokalne](#polecenia-lokalne)
    - [#WindowState](#windowstate)
    - [#ToBase64](#tobase64)
    - [#FromBase64](#frombase64)
    - [#EncodeData](#encodedata)
    - [#DecodeData](#decodedata)
    - [#GetMetadata](#getmetadata)
    - [#GetEncryptionData](#getencryptiondata)
    - [#GetInvoiceQrLink](#getinvoiceqrlink)
    - [#GetCertificateQrLink](#getcertificateqrlink)
    - [#CreateTestSeal](#createtestseal)
    - [#CreateTestSignature](#createtestsignature)
    - [#ValidateKsefNumber](#validateksefnumber)
    - [#GetTokenProperties](#gettokenproperties)
    - [#CertificateToPem](#certificatetopem)
    - [#CertificateFromPem](#certificatefrompem)
    - [#GetCertificateProperties](#getcertificateproperties)
    - [#ListStoreCertificates](#liststorecertificates)
    - [#CreateAuthRequest](#createauthrequest)
    - [#XadesSign](#xadessign)
  - [Polecenia serwera API](#polecenia-serwera-api)
    - [GetAuthChallenge](#getauthchallenge)
    - [SubmitXadesAuthRequest](#submitxadesauthrequest)
    - [GetAuthStatus](#getauthstatus)
    - [GetAccessTokens](#getaccesstokens)
    - [RefreshAccessToken](#refreshaccesstoken)
    - [OpenOnlineSession](#openonlinesession)
    - [SendOnlineSessionInvoice](#sendonlinesessioninvoice)
    - [CloseOnlineSession](#closeonlinesession)
    - [OpenBatchSession](#openbatchsession)
    - [CloseBatchSession](#closebatchsession)
    - [GetSessionStatus](#getsessionstatus)
    - [GetSessionInvoice](#getsessioninvoice)
    - [GetSessionInvoiceUpo](#getsessioninvoiceupo)
    - [ListSessionInvoices](#listsessioninvoices)
    - [ListSubjectInvoices](#listsubjectinvoices)
    - [GetInvoice](#getinvoice)
    - [SubmitInvoicesRequest](#submitinvoicesrequest)
    - [GetInvoicesRequestStatus](#getinvoicesrequeststatus)
    - [DownloadInvoices](#downloadinvoices)
    - [CreateKsefCsr](#createksefcsr)
    - [CompleteKsefCertificate](#completeksefcertificate)
    - [(pozostałe)](#pozostae)
  - [Polecenia serwera ToDo](#polecenia-serwera-todo)
    - [/posts](#posts)
    - [(pozostałe)](#pozostae)
- [Lista zmian w programie](#lista-zmian-w-programie)
  - [wersja 1.0.0.0](#wersja-1.0.0.0)
  - [wersja 0.9.0.5](#wersja-0.9.0.5)
  - [wersja 0.9.0.0](#wersja-0.9.0.0)
  - [wersja 0.8.0.0](#wersja-0.8.0.0)
  - [wersja 0.7.0.0](#wersja-0.7.0.0)
  - [wersja 0.6.0.0](#wersja-0.6.0.0)
  - [wersja 0.5.0.0](#wersja-0.5.0.0)
  - [wersja 0.4.0.0](#wersja-0.4.0.0)
  - [wersja 0.3.0.0](#wersja-0.3.0.0)
  - [wersja 0.2.0.0](#wersja-0.2.0.0)
  - [wersja 0.1.0.0](#wersja-0.1.0.0)
<!--/TOC-->
> [!NOTE]
> Ten opis został przygotowany w "dialekcie" *Markdown Document* obsługiwanym przez GitHub. Nie ma w nim wersji językowych dla symboli "Notatka" (*Note*), Wskazówka (*Tip*), Ostrzeżenie (*Caution*). Zdecydowałem się jednak korzystać z takich oznaczeń, bo skutecznie wyróżniają fragmenty tekstu. Dlatego nie dziw się, że te angielskie słowa występują po ikonie w ich pierwszych wierszach. 

## Wprowadzenie
**KSeF.Services**  komunikuje się z programem Klienta poprzez **nazwane potoki** (*named pipes*). Z punktu widzenia Klienta to pliki tekstowe, których ścieżki zawierają, oprócz nazwy pliku, specjalny przedrostek ("\\\\.\pipe\\*" <!-- "\\.\pipe\*" -->).

Serwer udostępnia 3 potoki, których nazwy ustala Klient uruchamiając aplikację serwera. (Klient podaje przedrostek nazw potoków w argumencie linii poleceń **--PipesPrefix**, por. opis [poniżej](#ref-pipes-prefix)). Nazwy potoków różnią się końcówkami (właściwie to ich "rozszerzenia nazw plików"):

* ***.in**: potok, do którego Klient wpisuje żądania KSeF (np. tekst "GetSessionStatus") oraz ewentualne parametry (jako tekst JSON)

* **.out**: potok, z którego Klient odczytuje odpowiedzi KSeF (jako tekst JSON)

* **.sta**: potok diagnostyczny - można z niego odczytać linię z informacją o aktualnym stanie serwera.
	(Czy i co teraz robi - chodzi o program **KsefServices.exe**, a nie serwer API KSeF po drugiej stronie sieci). Klient może go w ogóle nie otwierać, jeżeli nie potrzebuje takich informacji.

Każdy z tych potoków może obsłużyć tylko jedno połączenie (tzn. z instancją serwera może się połączć tylko jeden Klient). 

 Uruchomienie serwera z dysku lokalnego powinno trwać krócej niż sekundę, ale z dysku sieciowego może dochodzić do 3 sekund. (Program jest skompilowany jako całość bez zewnętrznych zależności, co w przypadku aplikacji .NET 9 oznacza konieczność załadowania podczas startu ok. 30 MB powiązanych komponentów *.dll*). Zużycie pamięci na dane jest poniżej 15MB.
 
 Sugerowany scenariusz to uruchamianie i kończenie *Ksef.Services* wraz programem Klienta.

> [!TIP]
> Jeżeli w Twoim rozwiązaniu działa naraz wiele instancji Klienta - każda z nich powinna uruchomić "swój" serwer *Ksef.Services.exe*. Nie ma obawy, Windows nie powieli ich *dll*-i (30 MB): będzie współdzielić komponenty załadowane przy pierwszym wywołaniu. Tylko rozdziel wtedy logi tych serwerów (por. parametr *--LogsFolder*, [poniżej](#ref-logs-folder)), bo inaczej wszystkie będą wpisywać się w ten sam plik.

## Podstawowa zasada komunikacji przez potoki nazwane

Obsługa potoków podstawowych - **.out** i **.in** - to "pływanie synchroniczne" Klienta i serwera. Cokolwiek jedna ze stron wpisała w potok, druga musi odczytać, i to w *tej samej kolejności*. Nadawca (raz jest o Klient, innym razem - serwer) zawsze ***czeka*** na odczytanie wpisanych danych.

Wyjątkiem jest potok diagnostyczny **.sta**. Nawet jeżeli Klient się do niego podłączył (czego nie musi robić), to przez cały czas działania programu może z niego nic nie czytać, bo wewnętrznie obsługuje go inny serwis. Jeżeli się podłączyłeś: pamiętaj tylko, by tuż przed wpisaniem do potoku *.in* polecenia zamknięcia programu ("END"), Klient odczytał pojedynczą linię z potoku *.sta*. W przeciwnym razie w zobaczysz w logu programu komunikat (wyjątek) o przerwanym połączeniu ("broken pipe") *.sta*. Można z tym żyć, ale skoro już otworzyłeś ten strumień, to "ładniej" jest pozwolić mu się zamknąć w sposób kontrolowany.

<a name="argumenty-linii-polecen-programu"></a>
## Argumenty linii poleceń programu

Nazwy argumentów programu wpisywane w linii poleceń można poprzedzać "--", albo "/", albo pisać *Nazwa=wartosc** lub *Nazwa="wartość ze spacjami"*. Wpisane w linię poleceń nadpisują ewentualne wartości domyślne lub pola o tych samych nazwach wpisane w plik *KSeF.Services.json*[^2].

**--PipesPrefix**:<a name="ref-pipes-prefix"></a>

Jedyny wymagany parameter programu: prefiks nazw potoków **.in*, **.out*, **.sta*. Powinien być unikalny dla aktualnej sesji Windows. (Dokładniej: w jednej sesji Windows nie powinny istnieć jednocześnie dwa potoki o takich samych nazwach). Dla uzyskania takiej unikalności wystarczy np. użyć w tym celu liczby milisekund od początku aktualnego dnia (same cyfry to też nazwa).

**--TargetUrl**:<a name="ref-target-url"></a>

Domyślnie: serwer testowy KSeF. Dla połączenia z KSeF zalecam podawać nazwy symboliczne odpowiedniego środowiska, czyli nie url-e, a 4-literowe teksty: **TEST**, **DEMO**, **PROD**. Można także uruchomić lokalny podzbiór poleceń serwera, poprzedzając taki symbol przedrostkiem "Local:" **"Local[:symbol]"**. Stąd ten "tryb lokalny" można wywołać wartościami: *Local:TEST*, *Local:DEMO* i *Local:PROD*. Każdy z nich będzie zwracać np. inne przedrostki adresów url zakodowanych w kodach QR. Wartość "Local" bez dwukropka i symbolu jest synonimem *Local:TEST*. Wreszcie, do jakichś zupełnie bazowych testów, można podać url [serwera ToDo](#polecenia-serwera-todo).

Ze względów praktycznych odradzam wpisywanie wartości *TargetUrl* w pliku konfiguracji programu (*KSeF.Services.json*). Trudniej wtedy o pomyłkę.
> [!NOTE] 
> Parametr **TargetUrl** najlepiej podawać jawnie, w linii poleceń wywołania *Ksef.Services*. 

**--CertsFolder**:<a name="ref-certs-folder"></a>

Parametr opcjonalny. Ścieżka do katalogu z kluczami publicznymi pobranymi z serwera. Może być względna (względem położenia pliku *Ksef.Services.exe*). Domyślnie: "..\certs". Musi istnieć.

**--LogsFolder**:<a name="ref-logs-folder"></a>

Parametr opcjonalny. Ścieżka do katalogu na dzienne pliki logów. Może być względna (względem położenia pliku *Ksef.Services.exe*). Domyślnie: "..\logs". Jeżeli wskazany folder nie istnieje - logi nie będą tworzone. 

--**OutputEncoding**:

Parametr opcjonalny. Domyślnie: standardowe ustawienia .NET Core (*"utf-8"*), ale dla starszych aplikacji Klienta (np. skryptów JScript) można podać *"windows-1250"*. 
Nazwy dostępnych enkodowań są dostepne [tutaj](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-text-encoding).

--**EOLReplacements:<CR|LF>**<a name="ref-eol-replacements"></a>

Parametry opcjonalne. W związku z tym, że w  komunikacji poprzez potoki nie możemy polegać na standardowych metodach *EndOfStream* lub podobnych, cała wymieniana paczka danych musi technicznie stanowić 1 linię. W związku z tym nadawca (Klient lub serwer) musi zamienić każdy znak CR i LF w przesyłanym tekście na ustalone znaki zastępcze, a odbiorca musi tę zamianę odwrócić.
W razie czego można te kody ustalić w pliku konfiguracyjnym *KSeF.Services.json*, w sekcji *"EOLReplacements":{ "CR" : , "LF": }* (por. przykład [poniżej](#ref-eol-example)).
Domyślnie to dwa specjalne znaki ASCII: "CR": 2, "LF": 3.

**--StatusFrequency**:

Parametr opcjonalny. Max. częstotliwość wpisywania stanu programu w potok *.sta*. (Możesz się do niej zbliżyć, gdy odczytujesz z niego co chwila, w pętli). Wyrażona w Hz , czyli "ile razy na sekundę". Domyślnie: 5. 

--**Settings**<a name="ref-settings"></a>

Parametr opcjonalny. Ścieżka do pliku _*.json_ z konfiguracją programu. Może być określona względem pliku Ksef.Services.exe. Może się kończyć na "\\*", jeżeli nazwa pliku konfiguracji to domyślne _**Ksef.Services.json**_. Wartość domyślna: "..\\\*" oznacza, że program będzie szukać pliku *Ksef.Services.json* "na zewnątrz" (..\\) folderu programu. Jeżeli we wskazanym miejscu nie ma pliku z konfiguracją - program przechodzi nad tym "do porządku dziennego" i podstawia dla wszystkich parametrów nie podanych w linii poleceń wartości domyślne. Jeżeli plik istnieje - jest wczytywany jako ostatnie źródło informacji (po wszystkich domyślnych zmiennych środowiskowych, plikach *appsettings.json* itp.). Oznacza to, że jego ustawienia mogą nadpisać te wcześniejsze.

>Przykładowa linia poleceń:<a name="przykadowa-linia-polecen"></a>
```
Ksef.Services.exe --PipesPrefix "8782325" --TargetUrl Local:TEST --LogsFolder "..\logs" --OutputEncoding "windows-1250"
```
>albo
```
Ksef.Services.exe --PipesPrefix "8782325" --TargetUrl DEMO --LogsFolder "..\logs" --OutputEncoding "windows-1250"
```
Specyficznym przypadkiem są testy połączenia REST. Aby je przeprowadzić, można wskazać w *--TargetUrl* adres serwera "ToDo" (to takie trochę inteligentniejsze "echo"):
```
Ksef.Services.exe --PipesPrefix "7781455" --TargetUrl "https://jsonplaceholder.typicode.com" --LogsFolder "..\logs"
```
>[!NOTE]
>Gdy łączysz się z https://jsonplaceholder.typicode.com pamiętaj, że obowiązuje wtedy zupełnie inny zestaw poleceń - por. [sekcja o serwerze ToDo](#polecenia-serwera-todo).

<a name="plik-z-konfiguracja-programu"></a>
## Plik z konfiguracją programu

Plik z konfiguracją programu domyślnie nosi nazwę *KSeF.Services.json* (por. opis argumentu *--Settings*, [powyżej](#ref-settings)). Można w nim na stałe wpisać każdy z argumentów programu:<a name="ref-eol-example"></a>
```json
{
  "LogsFolder":"..\\logs",
  "OutputEncoding":"windows-1250",
  "EOLReplacements": {
    "CR": 3,
    "LF": 2
  }
}
```
Można tu także umieścić inne elementy, np. związane ze szczegółami dziennika programu (por. [poniżej](#ref-log-settings)).

## Implementacja klienta Ksef.Services
W podkatalogu **.\Client** tego projektu umieściłem skoroszyt Excela z makrami (*.xlsm*). Znajdziesz w nim makro **Test**, uruchamiające program testowy napisany w Visual Basic For Applications (VBA).

Otwórz w tym Excelu IDE VBA (np. skrótem **[Alt]-[F11]**). Procedurę **Test** możesz uruchomić w panelu *Immediate* tego IDE, wpisując tam frazę "Test" i naciskając *[Enter]*. 

Umieszczone w kodzie VBA komentarze szczegółowo wyjaśniają rozwiązania zastosowane do komunikacji z serwerem. Implementacja bezpośredniej obsługi potoków nazwanych znajduje sie w klasie *PipesClient*. 
> [!NOTE]
> W implementacji tego Klienta nie wykorzystuję standardowej obsługi plików przez Basic (choć mógłbym). Zamiast nich używam strumienie (obiekty COM *TextStream*) z komponentu *Windows.Scripting.Runtime*. To obiekt stanowiący cześć Windows Scripting Host -  wykorzystywanego przez VBS czy JScript. Chciałem w ten sposób podkreślić uniwersalność zastosowanego rozwiązania - tego obiektu COM ("Scripting.FileSystemObject") można użyć w wielu innych językach programowania. 

Potoki *PipesClient* są w tym przykładzie wykorzystywane przez metody wyższego poziomu z modułu *KSeFServices*. (To ten moduł implementuje stronę Klienta dla komunikacji z serwerem Ksef.Services.exe)

W przykładowej sesji z Ksef.Services.exe, procedura *Test* (z modułu *Main*) wykorzystuje trzy metody publiczne z *KSeFServices*:

```vbnet
Sub Test()
    KSeFServices.Load Server:="https://jsonplaceholder.typicode.com" 
    
    Debug.Print KSeFServices.Send("/users/1") 
        
    KSeFServices.Dispose
End Sub
```
Jak widać w parametrach wywołamia metody *Load*, przedstawiony powyżej kod łączy się ze specjalnym serwerem służacym do najprostszych testów działania komunikacji REST (patrz [tutaj](#polecenia-serwera-todo)). Wywołanie tej procedury VBA wyświetla w oknie *Immediate* IDE następujący wynik:
```
Test
Upłynęło: 0,19s, wykonano wcześniej 4 nieudanych prób połączenia z '\\.\pipe\7781455.out'
Upłynęło:: 0,05s, wykonano wcześniej  1 nieudanych prób połączenia z '\\.\pipe\7781455.in'
{
  "id": 1,
  "name": "Leanne Graham",
  "username": "Bret",
  "email": "Sincere@april.biz",
  "address": {
    "street": "Kulas Light",
    "suite": "Apt. 556",
    "city": "Gwenborough",
    "zipcode": "92998-3874",
    "geo": {
      "lat": "-37.3159",
      "lng": "81.1496"
    }
  },
  "phone": "1-770-736-8031 x56442",
  "website": "hildegard.org",
  "company": {
    "name": "Romaguera-Crona",
    "catchPhrase": "Multi-layered client-server neural-net",
    "bs": "harness real-time e-markets"
  }
}
```

Oczywiście, nic nie stoi na przeszkodzie, by pomiędzy *Load* i *Dispose* znajdowało się więcej wywołań funkcji *Send()*. W produktywnym rozwiązaniu metoda *Load* jest wołana gdzieś na początku programu, a *Dispose* - gdy kończy działanie.

W powyższym przykładzie procedura *KsefServices.Load* otworzyła dwa potoki (*.out* i *.in*), czyli ten Klient komunikował się z serwerem synchronicznie (tzn. nie korzystał z potoku *.sta*). Wyświetlone u góry informacje nieudanych połączeniach są normalne. W implementacji klasy *PipesClient* zdecydowałem się rozpocząć próby otwarcia potoku natychmiast po wywołaniu pliku *.exe serwera. Zrobiłem to z całą swiadomością, że kilka pierwszych będzie nieudanych, bo aplikacja potrzebuje ułamek sekundy na załadowanie bibliotek ( *.dll) i inne czynności startowe. To nieeleganca, ale szybsza metoda od "kulturalnego" odczekania jakiegoś stałego interwału czasu (powiedzmy - sekundy).

Klient powinien otwierać potoki tak, jakby to były pliki tekstowe o różnych rozszerzeniach, umieszczone na specyficznej "ścieżce". Poniżej podaję je dla serwera wywołanego tak, jak w linii poleceń przedstawionej w przykładzie [powyżej](#przykadowa-linia-polecen):
  <!--  Uwaga redakcyjna: dla przegladarki MD znaki \ muszę (zazwyczaj) wpisywać podwójnie. W komantarzu podaję właściwą postać tych tekstów -->
* dla **.out**: to: *"\\\\.\pipe\7781455.out"* <!-- "\\.\pipe\7781455.out" --> (otwórz do odczytu, w przykładowym kliencie jest w instancji *PipesClient* o nazwie *myInput*) 
* dla **.sta**: to: *"\\\\.\pipe\7781455.sta"* <!-- "\\.\pipe\7781455.sta" --> (otwórz do odczytu, w przykładowym kliencie  jest w instancji *PipesClient* o nazwie *myStatus*) 
* dla **.in**: to: *"\\\\.\pipe\7781455.in"* <!-- "\\.\pipe\7781455.in" --> (otwórz do zapisu, w przykładowym kliencie  jest w instancji *PipesClient* o nazwie *myOutput*) 
> [!CAUTION]
> Te potoki Klient musi <u>otwierać w takiej kolejności, jak powyżej</u>. Może pominąć otwarcie potoku **.sta**, jeżeli nie będzie z niego korzystać.

Metoda Klienta *KsefServices.Send* wysyła żądanie do serwera, wpisując jego symbol jako nową linię (np. metodą *WritLine*) do potoku **.in**. Potem, jeżeli żądaniu towarzyszą parametry, wpisuje do tego potoku ich wartości jako drugą linię. (Zazwyczaj to jakiś słownik JSON).

```vbnet
Public Function Send(ByVal request As String, Optional ByVal arguments = "") As String
    Dim result
    myOutput.WriteLine request
    If arguments <> "" Then myOutput.WriteLine arguments
    result = myInput.ReadLine
    Send = result
End Function
```

Następnie w metodzie *Send* wywoływany jest odczyt linii (funkcją *ReadLine*) z potoku *.out*. Opuszczenie tej linii programu Klienta nastąpi dopiero po zakończeniu przetwarzania polecenia przez serwer i wpisaniu rezultatu do potoku *.out*. (Czyli wtedy, gdy Klient będzie miał co odczytać). Wczytana linia też jest w formacie JSON.

W komunikacji przez potoki strona wpisująca czeka na linii z poleceniem zapisu (tu: *WriteLine*) dopóki druga strona nie odczyta tej informacji. Serwer najpierw wczytuje z potoku *.in* otrzymane od Klienta żądanie (to pierwsza linia). Następnie tworzy przypisany do tego żądania serwis obsługi (obiekt .NET), który informuje serwer, czy oczekuje jakichś parametrów. Jeżeli tak, to serwer wczytuje z potoku *.in* drugą linię, która powinna być tam wpisana przez Klienta. Przekazuje te dane obiektowi obsługi żądania, który je przetwarza. Rezultat przetworzenia serwer wpisuje jako jedną linię w potok *.out*. Na "drugim końcu" tego potoku Klient oczekuje już na te dane (w linii z poleceniem *ReadLine*)

W argumentach, które przekazuje Klient, jak i w odpowiedzi, jaką odsyła serwer, mogą pojawić się znaki nowych linii (CRLF). W związku z tym, że przyjętą w tej komunikacji "jednostką" odczytu/zapisu jest linia, trzeba wszystkie CRLF w zapisywanym tekście zamienić na jakieś znaki neutralne (por. [argumenty](#ref-eol-replacements) / [plik konfiguracji](#ref-eol-example) programu). Te znaki przy odczycie są z powrotem zamieniane na CRLF. Służą do tego pomocnicze funkcje *AsSingleLine()* i *AsMultiLine()*:
```vbnet
Public Function Send(ByVal request As String, Optional ByVal arguments = "") As String
    Dim result
    myOutput.WriteLine request
    If arguments <> "" Then myOutput.WriteLine AsSingleLine(arguments)
    result = AsMultiLine(myInput.ReadLine)
    Send = result
End Function
```
Pierwsze wywołanie *WriteLine* (z symbolem polecenia) nie zawiera nigdy znaku nowej linii, więc pozostawiłem je bez tej konwersji.

W VBA nie ma mechanizmu związanego ze słowem kluczowym *await*, które występuje w nowoczesniejszych językach programowania. Powoduje to duże komplikacje w implemetacji. Poniżej przedstawiam wersję tej samej funkcji, z dodaną obsługą quazi-asynchronicznej komunikacji opartej o potok diagnostyczny. Wykorzystuję tu pomocniczą funkcję *Inquiry*, która odczytuje z potoku diagnostycznego *.sta* aktualny stan *KSef.Servcices.exe* (por. opis w [następnej sekcji](#informacje-zwracane-przez-potok-diagnostyczny)). Znając stan serwera, funkcja *Send* może odpowiednio zmodyfikować swoje działanie:<a name="ref-send-sync1"></a>

```vbnet
Public Function Send(ByVal request As String, Optional ByVal arguments = "") As String
    Dim result
    Dim state
    state = Inquiry("STBY") '"zjadamy" ewentualne stany STBY ("Stand By" - oczekiwanie na żądanie)
    myOutput.WriteLine request
    If arguments <> "" 
        state = Inquiry("WRTE") 'Czekamy na stan WRTE (możliwość wpisania danych)
        If Left(state, 4) = "WRTE" Then
            myOutput.WriteLine AsSingleLine(arguments)
        Else
            Debug.Print "(!?) żądanie """ & request & """ nie odczytało argumentów """ & arguments & """"
        End If
    End If
    'Teraz serwer przechodzi przez stany PRCS, WAIT, a my zaczynamy czekać, kiedy go zmieni na READ:
    state = Inquiry("READ")
    'Odczytaj i zwróć odpowiedź serwera:
    'UWAGA: serwer nie przejdzie do dalszych operacji, dopóki nie odczytasz zwróconej linii!
    If Left(state, 4) = "READ" Then
        result = AsMultiLine(myInput.ReadLine)
    End If
    Send = result
End Function
```
Wewnątrz funkcji _**Inquiry**_ jest wołane wrażenie VBA *DoEvents*, przekazujące sterowanie programowi, aby obsłużył w UI działania użytkownika. Gdy Klient został uruchomiony w trybie synchronicznym, *Inquiry* nie robi niczego, poza natychmiastowym zwróceniem otrzymanego parametru jako swojego rezultatu. Szczegółowe wyjaśnienie jej implementacji znajdziesz w komentarzach umieszczonych w kodzie VBA modułu *KsefServices*. 
> [!NOTE]
> W przedstawionym rozwiązaniu asynchroniczna komunikacja serwera z Klientem przebiega dużo wolniej, bo często wywołanie funkcji *Inquiry* musi poczekać na "opublikowanie" przez serwer aktualnego stanu. Domyślnie następuje to co 1/5 sekundy (steruje tym parametr *--StatusFrequency*). Takie rozwiązanie oparte o potok diagnostyczny to "proteza" prawdziwej asynchroniczności. 

> [!TIP] 
> Jeżeli tylko Twój język programowania zawiera mechanizm *await*, użyj go w przedstawionej wcześniej "synchronicznej" wersji funcji *Send*. To nie spowoduje spowolnień.

Na koniec dodatkowy drobiazg: linię żądania i jej parametry możesz wysłać w jedej instrukcji *WriteLine*. Wystarczy, że rozdzielisz je - to bardzo ważne - tabulacją (ASCII #09)i znakiem nowej linii (CRLF):
```vbnet
Public Function Send(ByVal request As String, Optional ByVal arguments = "") As String
    Dim result
    If arguments = "" Then 
		myInput.WriteLine request
	Else 'Wyślij w jednym poleceniu żądanie wraz z argumentami, 
		 'rozdzielone tabulacją i symbolem nowej linii:
		myInput.WriteLine request & vbTab & vbCrLf & AsSingleLine(arguments)
	End if
    result = AsMultiLine(myInput.ReadLine)
    Send = result
End Function
```
Jednokrokowe wysyłanie żądania i jego argumentów ma znaczenie w przypadku trybu synchronicznego. Gdybyś w pierwszej, "dwukrokowej" wersji funkcji *Send* (takiej jak [tutaj](#ref-send-sync1)) wpisał w trybie synchronicznym nazwę żądania z literówką, serwer odnotowałby błąd, po czym utknąłby z "deadlocku" z Klientem. 

Ta blokada pojawiała się w "dwukrokowym" zapisie, bo po wysłaniu pierwszej linii, Klient wpisywał w drugiej linii z *WriteLine* argumenty żądania i czekał na ich odczyt przez serwer. Tej linii serwer jednak już nie odczytywał, gdy pierwsza linia spowodowała błąd. Serwer przechodził wtedy od razu do wpisania w potok *.out* szczegółów błędu. I tam sam zaczynał czekać, bo Klient czekający na odczyt poprzedniej linii (drugie *WriteLine*) nigdy nie docierał do odpowiedniej instrukcji odczytu (*ReadLine*). 

Dzięki wpisaniu po stronie Klienta w jednym kroku obydwu linii, dane są już wysłane, a Klient czeka w tej jedynej instrukcji *WriteLine* na dokończenie ich odczytu przez serwer. Znak tabulacji "doklejony" do symbolu żądania to sygnał dla serwera, że ma jeszcze coś do odczytania, niezależnie od tego, czy wystąpił błąd albo co konfabuluje błędnie napisany serwis obsługi żądania. Gdy to zrobi, obydwie strony zgodnie przejdą do zapisu i odczytu informacji o błędzie.

## Informacje zwracane przez potok diagnostyczny

> [!TIP]
> Ta sekcja jest opcjonalna. Zawiera dodatkowe informacje, które mogą być przydatne przy rozwiązywaniu ewentualnych problemów, które pojawiły się podczas implementacji Klienta.


W prostej implementacji Klienta nie trzeba w ogóle korzystać z informacji dostarczanych przez potok diagnostyczny **.sta**. Mogą się jednak przydać podczas debugowania nowych rozwiązań. 

Zacznijmy od wewnętrznej architektury **Ksef.Services.exe**. W aplikacji działają dwa serwisy, o nazwach:

* **Worker**: serwis podstawowy. Komunikuje się poprzez potoki *.in* i *.out*, obsługując przesyłane przez Klienta żądania.
* **Indicator**: serwis pomocniczy. Zapisuje informacje o stanie programu do potoku *.sta*.

Każdy z serwisów działa wtedy, gdy drugi czeka na zakończenie jakiejś operacji. Najczęściej oczekują na odczytanie przez Klienta informacji ze swoich potoków wyjściowych. *Worker* dodatkowo może także czekać na przysłanie przez Klienta nowego żądania, wpisanie danych wejściowych (jeżeli są wymagane) lub zwrot informacji przez wysłane żądanie HTTP. Podczas inicajlizacji obydwa serwisy oczekują także na przyłączenie przez Klienta potoków, które udostępniają.

Jeżeli Klient podłączył się do potoku *.sta*, to *Indicator* wpisuje do niego pierwszą linię ze stanem programu i przekazuje sterowanie do *Worker*. Zaczyna czekać na ewentualne odcztanie tej linii przez Klienta. Gdy Klient odczytał tę linię - wpisuje następną. Robi to z ustaloną max. częstotliwością (por. parametr *--StatusFrequency* linii poleceń programu).

Gdy serwis *Worker* oczekuje na żądanie, linia odczytana przez Klienta z potoku *.sta* wygląda jak poniżej (oczywiście, data i czasy będą w niej inne):
```
"STBY    (set: 21:42:36.144) was at 2025-07-10 21:42:36.147"
```
W tekście powyżej:
* Znaki 1-4[^3] to **4-literowy skrót stanu programu**. W tym przypadku to **STBY** to skrót od "Stand by": oczekuje na polecenie.
* Znaki 5-7 to symbole **"poziomu zajęcia"** serwisu obsługą jakiegoś zgłoszenia (patrz poniżej). Trzy spacje oznaczają "wymagane działanie Klienta".
* Znaki 15-26 to czas (lokalny), informujące od kiedy serwis jest w podanym stanie.
* Ostatnie 23 znaki (z prawej) to lokalna sygnatura czasowa zapisania tej linii do potoku *.sta*.

> [!TIP]
> Lokalna sygnatura czasowa (ostatnie 23 znaki tekstu) jest "jak data gazety": pozwala Klientowi zorientować się, czy otrzymana informacja o stanie serwera jest aktualna. Zazwyczaj pierwsza linia odczytana z potoku *.sta* jest już "przeterminowana". Dopiero kolejna zawiera aktualną informację.[^4] 

Gdy serwer (a dokładnie - serwis *Worker*) otrzyma żądanie, zmienia stan na **NREQ** ("New request"):
```
"NREQ.   (set: 21:42:36.555, '<żądanie>') was at 2025-07-10 21:42:36.556"
```
Pierwszy z trzech znaków **poziomu zajęcia** (znak nr 5) zmienia się na kropkę, a w prawej części nawiastu pojawia się tekst otrzymanego od Klienta żądania.\
Teraz *Worker* tworzy nowy *RequestHandler* - obiekt klasy przypisanej do obsługi tego żądania. Może zgłosić wyjątek (błąd), jeżeli go nie znajdzie.

Gdy *Worker* stworzył obiekt do obsługi żądania, pyta go, czy oczekuje od Klienta dodatkowych danych (tzn. parametrów żądania). Jeżeli otrzyma potwierdzenie, to kolejnym stanem serwera jest **WRTE** ("Write"):
```
WRTE..  (set: 21:42:36.566, '<żądanie>' is being handled by <klasa obsługująca>) was at 2025-07-10 21:42:36.558"
```
Ten stan oznacza, że *Worker* przechodzi do odczytania z potoku *.in* dodatkowej linii danych, wpisanych przez Klienta. (Klient mógł to zrobić ze swojej strony wcześniej, zaraz po wpisaniu linii z nazwą żądania).

Zwróć uwagę, że traz dwa z trzech znaków **poziomu zajęcia** (znaki nr 5 i 6) to kropki.

> [!CAUTION]
Jeżeli Klient nie wpisze do potoku *.in* wymaganych przez program parametrów żądania, serwis *Worker* "zawiśnie", czekając na tę akcję.
 
 Gdy *Worker* otrzymał dane wejściowe, zmienia stan serwera na **PRCS** ("Processing"). Następnie przekazuje te dane obiektowi obsługującemu żądanie:
 ```
"PRCS... (set: 21:42:45.863, '<żądanie>' is being handled by <klasa obsługująca>) was at 2025-07-10 21:42:45.865"
```
W tym stanie obiekt obsługujący sprawdza poprawność otrzymanych danych i dokonuje ewentualnych uzupełnień (np. liczy skróty plików, szyfruje dane przed wysłaniem, itp.). Może zgłosić wyjątek, jeżeli np. tekst danych wejściowych ma niepoprawny format.\
Zwróć uwagę, że teraz wszystkie trzy znaki **poziomu zajęcia** (5, 6 i 7) to kropki.

Gdy dane zostały przetworzone, *Worker* zmienia stan serwera na **WAIT**, a obiekt obsługujący wysyła odpowiednie żądanie REST do serwera API:
 ```
"WAIT... (set: 21:42:45.867, '<żądanie>' is being handled by <klasa obsługująca>) was at 2025-07-10 21:42:45.868"
```
W tym stanie oczekuje na odpowiedź serwera API. To może potrwać np. pół sekundy. Gdy obiekt obsługujący otrzyma zwrotnie informację o błędzie, zgłosi serwerowi odpowiedni wyjątek. Jeżeli wszystko przebiegło poprawnie - zwróci otrzymane informacje serwisowi *Worker*.

Ostatnim ze stanów tego cyklu jest **READ**:
```
"READ    (set: 21:42:48.865, '<żądanie>') was at 2025-07-10 21:42:48.869"
```
<u>Stan *READ* wystąpi w każdym przypadku</u>: i gdy w czasie przetwarzania któregokolwiek ze stanów wcześniejszych wystąpił wyjątek (błąd), i jeżeli wszystko przebiegło poprawnie. Oczywiście, w przypadku błędu Klient odczyta z potoku jego szczegóły, a w przypadku powodzenia - dane otrzymane z serwera API. Jeżeli wywołane żądanie nie zwraca żadnych danych - będzie to pusta lista: "\{\}" <!-- "{}" -->.\
Zwróć uwagę, że traz wszystkie trzy znaki **poziomu zajęcia** (5, 6 i 7) to spacje. To sygnał, że serwer (a dokładnie, jego serwis *Worker*) oczekuje od Klienta wczytania wpisanych w potok danych. 

Po wczytaniu danych przez Klienta, serwer przechodzi z powrotem w stan *STBY*.

> [!NOTE]
> W sekwencji przedstawionej powyżej (**NREQ**-**WRTE**-**PRCS**-**WAIT**-**READ**) stany **WRTE** i **PRCS** mogą nie wystąpić, gdy otrzymane żądanie nie wymaga podania żadnych parametrów. Wtedy podczas przetwarzania żądania serwer przejdzie tylko przez trzy stany: **NREQ**-**WAIT**-**READ**.

## Dzienniki programu
Ksef.Services tworzy dzienne pliki z logiem aktywności. Domyślnie znajdują się w folderze *..\logs*. (Jego położenie jest określane względem katalogu z plikiem *Ksef.Services.exe*). Możesz wskazać inny folder, podając go w parametrach wywołania programu (*--LogsFolder*, por [tutaj](#ref-logs-folder)) lub wpisując do pliku konfiguracji (*KSeF.Services.json*):
```json
{
  "LogsFolder": "C:/KSeF/log",
  ...
}
```
>[!NOTE]
>Pojedynczy plik dziennika zawiera zapisy sesji *Ksef.Services*, które rozpoczęto w danym dniu (nawet, jeżeli zakończyły się już w następnym).

> [!TIP]
> Aby wyłączyć logowanie do pliku, wskaż foler, który nie istnieje.

Zapisy pojawiające się w konsoli Ksef.Services to druga, ulotna forma dziennika, która znika wraz z zamknięciem tego okna.

Ten sam wpis w pliku dziennika i konsoli różni się szczegółami.\
Konsola:
```
info: KSeF.Services.Worker[0]
      Received new request: '/posts'
```
Plik:
```
Info	2025-07-23 08:16:50.059 <KSeF.Services.Worker> after 206ms:
	Received new request: '/posts'
```
Każdy wpis dziennika składa się z dwóch lub wiecej linii. W pierwszej linii znajdują się informacje "porządkowe":
* Typ zapisu. Może tu być: ***Critical***, ***Error***, ***Warning***, ***Info***rmation, ***Debug***, ***Trace***. Określa wagę / poziom istotności informacji.
* Data i czas zdarzenia (brak w przedstawionym wpisie z konsoli)
* Klasa obiektu, który odnotowuje to zdarzenie. To pełna nazwa klasy (wraz z przestrzenią nazw). W przykładzie powyżej to serwis *Worker*.
* Czas ("after"), który upłynął od poprzedniego wpisu **tego obiektu**. W przykładzie powyżej od poprzedniego zapisu odnotowanego przez serwis *Worker* upłynęło 206ms. Tej informacji nie ma w konsoli. 

W drugiej (i ewentualnie kolejnych) liniach wpisu znajduje się właściwy przekaz.

> [!TIP]
> Informacja o czasie od poprzedniego wpisu dokonanego przez obiekt ułatwia orientację w ewentualnych opóźnieniach. Na przykład, żądania i odpowiedzi HTTP są w KSef.Services odnotowywane przez specjalny obiekt *HttpRequestsObserver*. Podany we wpisie do logu z otrzymanym rezultatem żądania HTTP (*HTTP response*) czas "after" to informacja, jak długo program czekał na odpowiedź serwera KSeF.

Podobnie, czas "after" z zapisu o rozpoczęciu zamykania alikacji, odnotowany przez obiekt *Microsoft.Hosting.Lifetime*, informuje o całkowitym czasie tej sesji Ksef.Services:<a name="ref-shutdown-log"></a>
```
Info	2025-07-23 08:16:50.690 <Microsoft.Hosting.Lifetime> after 17.744s:
	Application is shutting down...
```
Korzystając z funkcji wbudowanych w standardowego hosta serwisów .NET, można ustalić inny poziom szczegółowości logowania dla każdego z obiektów "wpisujących się" do dziennika. Służy do tego sekcja **"Logging"** pliku *KSeF.Services.json*: <a name="ref-log-settings"></a>
```json
{
  "LogsFolder": "C:/KSeF/log",
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "KSeF.Services.Indicator": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "KSeF.Services.HttpRequestsObserver": "Debug"
    },
    "Console": {
      "LogLevel": {
        "Default": "Information"
      },
      "FormatterName": "simple",
      "FormatterOptions": {
        "IncludeScopes": false
      }
    }
  },
  ...
}
```
Jej podsekcja **"LogLevel"** pozwala ustalić domyślny poziom szczegółowości dziennika, oraz wprowadzić inne poziomy dla wybranych klas.\
Te ustawienia obowiązują każde z "mediów": plik i konsolę, chyba że dopiszesz oddzielną podsekcję **Console**. Możesz w niej ustawiać inne poziomy szczegółowości, wyłącznie dla tego medium.

W pliku *KSeF.Services.json* pokazanym powyżej, klasa *Microsoft.Hosting.Lifetime* ma przypisany poziom **Information**, i w konsoli, i w pliku. Na tym poziomie wykonuje tylko dwa wpisy: pierwszy o uruchomieniu aplikacji, i drugi, o jej zamknięciu (pokazany na poprzednim przykładowym [wpisie z logu](#ref-shutdown-log)). Stąd jej czas "after" to czas sesji programu, bez sekwencji startowej i zamknięcia.

> [!TIP]
> Śledzenie żądań i odpowiedzi HTTP działa od poziomu **Debug**. Aby w dzienniku zostały odnotowane także nagłówki HTTP użyte w komunikacji, ustaw poziomu **Trace**, przynajmniej dla klasy "**KSeF.Services.HttpRequestsObserver**".

> [!NOTE] 
> Lepiej pozostawić klasę "**KSeF.Services.Indicator**" na poziomie **Information** lub wyższym. Jeżeli domyślnym poziomem jest *Debug* lub *Trace*, odnotuj to jako odpowiedni wyjątek w *.json*. W trybie **Trace** *Indicator* bombarduje informacjami o tym, że Klient odczytał status serwera ze strumienia **.sta*. Taka możliwość może się przydać tylko do debugowania jakichś szczególnych przypadków.

<a name="lista-polecen-ksef.services"></a>
# Lista poleceń Ksef.Services
Program zawiera trzy zestawy poleceń. Każde z nich jest związane z serwerem, wybranym a argumencie **--TargetUrl** (por. [tutaj](#ref-target-url)). 
> [!NOTE]
> W nazwach poleceń duża i mała litera jest traktowana jako ten sam znak.

> [!TIP]
> W argumentach poleceń można zawsze podawać względne ścieżki do plików. Położenie takich plików jest określane względem katalogu z plikiem *Ksef.Services.exe*.

<a name="polecenia-wspolne"></a>
## Polecenia wspólne
Polecenia, które można stosować niezależnie od wybranego serwera.
### END 
To polecenie kończy działanie serwera (tzn. zamyka w sposób kontrolowany program *Ksef.Services.exe*). 

_[=> spis treści](#spis-tresci)_

ARGUMENTY: brak

REZULTAT: nie zwraca żadnych danych, bo kończy działanie programu.

UWAGI: \
Po wywołaniu polecenia **END** Klient może odczekać chwilkę (np. pół sekundy) i zamknąć wszystkie otwarte strumienie potoków nazwanych.

  > [!IMPORTANT]
  Jeżeli masz otwarty potok *.sta*, to tuż przed wysłaniem polecenia END odczytaj z niego linię (np. funkcją *ReadLine*). (Potok *.sta* zawsze zawiera tylko jedną linię). Jeżeli o tym zapomnisz, w logu programu zobaczysz komunikat o błędzie "broken pipe" podczas kończenia działania aplikacji.
## Polecenia lokalne
Polecenia dostępne w trybie offline (*TargetUrl*=**Local[:symbol]**), nie wymagające połączenia z siecią. Symbol tego trybu to np. *Local:TEST* dla środowiska testowego, albo *Local:PROD* dla generowania kodów QR faktur wystawionych w trybie offline.

### #WindowState
Zwraca i (ewentualnie) zmienia stan okna konsoli programu

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _setTo_ <= zawsze\
Liczba całkowita (jedna ze flag SW_* używanych w wywołaniu Win API _ShowWindow()_): 0 = HIDE, 1: NORMAL, 3: MAXIMIZE, 6: MINIMIZE). Jeżeli przekażesz wartość spoza ich zakresu (0..10), to wartość _setTo_ zostanie zignorowana.

Przykład:
```json
	{"setTo":1}
```
REZULTAT:
* _before_ <= zawsze \
stan okna w przed wywołaniem tego żądania. Liczba całkowita o takim samym znaczeniu jak _setTo_

Przykład:
```json
	{"before":0}
```
>[!TIP]
>Wywołuj to żądanie z wartością _setTo_ = -1, aby tylko sprawdzić aktualny stan okna konsoli programu. (W istocie to nie musi być -1, tylko dowolna liczba spoza zakresu 0..10).

### #ToBase64
Dokonuje konwersji danych na ciąg bajtów enkodowany w Base64.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _byteArray_ \
lista liczb całkowitych - wartości bajtów (0..255), np.:
```json
	{"byteArray":[1,2,3,4]}
```
* _text_ \
tekst (string), np.:
```json
	{"text":"ala ma kota"}
```
REZULTAT:
* _base64_ <= zawsze \
enkodowany tekst (string), np.:
```json
	{"base64":"AQIDBA=="}
```

### #FromBase64
Dokonuje konwersji danych ciągu bajtów enkodowanych w Base64 na tekst lub tablicę liczb dziesiętnych (wartości bajtów).

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _base64_ <= wymagane \
enkodowany tekst (string)
* _asBytes_ \
*true*, gdy rezultat ma być zwrócony jako lista wartości bajtów, np.:
```json
	{"asBytes":true,"base64":"AQIDBA=="}
```
domyślnie: *false*, więc po prostu pomijaj to pole, gdy chcesz przekształcić dane na tekst, np.:
```json
	{"base64":"YWxhIG1hIGtvdGE="}
```
zwróci rezultat jako tekst (string)

REZULTAT:
* _byteArray_ \
lista liczb całkowitych - wartości bajtów (0..255) np.:
```json
	{"byteArray":[1,2,3,4]}
```
zwracany, gdy podano w argumentach *asBytes* = *true*
* _text_ \
tekst (string), np.:
```json
	{"text":"ala ma kota"}
```
zwracany gdy podano w argumentach *asBytes* = *false*, lub pominięto to pole.

### #EncodeData
Szyfruje otrzymane dane. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY: 
* _plainBase64_ <= wymagane \
(string) ciąg danych, enkodowany w Base64
* _pwd_ \
tekst hasła, jeżeli ma być użyte szyfrowanie AES.
* _mix_ \
dodatkowy kod zabezpieczający dla szyforwania DAPI. Ciąg bajtów, enkodowany w Base64. Jeżeli go nie podasz, procedura wygeneruje tę wartość w sposób losowy. _mix_ jest zwracany w strukturze rezultatu, niezależnie od tego, czy został wygenerowany, czy też podany jawnie.
* _noMix_ \
ustaw na *true*, gdy nie chcesz rezultatu z dodatkowym kodem zabezpieczającym (*mix*), np:
```json
{"noMix":true,"plainBase64":"V3ljennFm8SHIHByemVkcG9rw7Nq"}
```
Domyślnym ustawieniem jest *false*, wtedy możesz to pole pominąć:
```json
{"plainBase64":"V3ljennFm8SHIHByemVkcG9rw7Nq"}
```

>[!TIP]
>Jeżeli podasz parametr _pwd_, to dane będą zaszyfrowane algorytmem AES256, a ewentualny argument _mix_ - zignorowany. Gdy nie podałeś _pwd_, dane są szyfrowane przez DAPI.

REZULTAT: 
* _encryptedBase64_ <= zawsze \
zaszyfrowane dane, np.:
```json
{
	"encryptedBase64":"AQAAANCMnd8BFdERjHoAwE/Cl\u002BsBAAAAPbl...GvzBXWGbOLUgwSjngQ7uP\u002BQkO4/E"
}
```
gdy w argumentach podano _noMix_=*true*. \
* _mix_ \
dodatkowy kod zabezpieczający (enkodowany w Base64). Należy go przekazać w argumentach wywołania *#DecodeData* wraz z zaszyfrowanymi danymi (_encryptedBase64_).
Pojawia się, gdy w argumentach pominięto pole _noMix_ (lub jawnie ustawiono je na *false*) np.:
```json
{
	"mix":"ghTN4mB\u002Br4aL9Vvszhu0yg==",
	"timestamp":"2025-07-29T18:31:33.770Z",
	"encryptedBase64":"AQAAANCMnd8BFdERjHoAwE/Cl\u002BsBAAA...MX4zR\u002BfIITp"
}
```
* _timestamp_ \
(string) znacznik czasu (UTC). Towarzyszy zawsze polu _mix_ (por. przykład powyżej). Dodałem go na wszelki wypadek.

UWAGI: 
* Zaszyfrowane dane mogą być dłuższe od danych źródłowych (tekstu _plainBase64_).
* Dla większego bezpieczeństwa, wartość _mix_ i odpowiadające jej zaszyfrowane dane najlepiej przechowywać w różnych miejscach (np. bazy danych). Obydwu takim wpisom może towarzyszyć _timestamp_. Jego obecność pozwoli upewnić się przed dekodowaniem, że odczytany z bazy danych _mix_ i _encryptedBase64_ są ze sobą powiązane.
* Gdy metoda wykorzystuje DAPI Windows, to szyfrowanie i odszyfrowywanie należy przeprowadzać po zalogowaniu się jako ten sam użytkownik.

### #DecodeData
Odszyfrowuje dane, zaszyfrowane za pomocą **#EncodeData**.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _encryptedBase64_ <= wymagane \
zaszyfrowane dane, uzyskane z *#EncodeData* np.:
```json
{
	"encryptedBase64":"AQAAANCMnd8BFdERjHoAwE/Cl\u002BsBAAAAPbl...GvzBXWGbOLUgwSjngQ7uP\u002BQkO4/E"
}
```
gdy szyfowałeś bez dodatkowego zabezpieczenia \
* _pwd_ \
tekst hasła, jeżeli do szyfrowania użyto algorytmu AES.
* _mix_ \
dodatkowy kod zabezpieczający (enkodowany w Base64). Wartość zwrócona przez *#EncodeData* wraz z zaszyfrowanymi danymi (_encryptedBase64_). np.:
```json
{
	"mix":"ghTN4mB\u002Br4aL9Vvszhu0yg==",
	"encryptedBase64":"AQAAANCMnd8BFdERjHoAwE/Cl\u002BsBAAA...MX4zR\u002BfIITp"
}
```

>[!TIP]
>Jeżeli podasz parametr _pwd_, to dane będą traktowane jako zaszyfrowane algorytmem AES256, a ewentualny argument _mix_ - zignorowany. Gdy nie podałeś _pwd_, dane są odszyfrowane przez DAPI.

REZULTAT:
* _plainBase64_ <= zawsze \
Odszyfrowane dane, enkodowane w Base64, np.:
```json
{"plainBase64":"V3ljennFm8SHIHByemVkcG9rw7Nq"}
```
UWAGI:
* Należy zwrócić uwagę, aby podać prawidłową wartość _mix_ - dokładnie taką, jaką zwróciła metoda _#EncodeData_. W przeciwnym razie odszyfrowanie się nie powiedzie!

### #GetMetadata
Zwraca skrót (hash) i rozmiar (w bajtach) zawartości wskazanego pliku lub ciągu bajtów. \

_[=> spis treści](#spis-tresci)_

ARGUMENTY: 
* _filePath_ \
ścieżka do pliku, który ma być "zmierzony". Może być względna, np.:
```json
{"filePath":"..\\logs\\2025-07-28_log.txt"}
```
* _base64_ \
ciąg bajtów (dane binarne), które mają być "zmierzone". Enkodowany w Base64:
```json
{"base64":"AQIDBA=="}
```
REZULTAT:
* _hashSHA_ <= zawsze \
Skrót SHA256 zawartości pliku / danych, enkodowany w Base64.
* _fileSize_ <= zawsze \
Długość pliku / danych, w bajtach.
Przykład:
```json
{
	"hashSHA":"TJF1YWseJ/SIs4\u002BeY25bsm\u002BtTxp0A/H7PYbCgu17024=",
	"fileSize":21167
}
```
UWAGI:
* Aby obliczyć poprawny skrót pliku XML, wskaż go poprzez nazwę pliku. Nie pobieraj jego zawartości do obiektu XmlDocument, aby potem obliczyć skrót jego zawartości zamienionej na tekst / tablice bajtów, bo otrzymasz inny wynik.

### #GetEncryptionData
Zwraca nowy (losowy) klucz symetryczny: w oryginale (do zastosowania) i zaszyfrowany kluczem publicznym (do przekazania do którejś z metod KSeF) 

_[=> spis treści](#spis-tresci)_

ARGUMENTY: brak

REZULTAT:
* _cipherKey_ <= zawsze \
klucz symetryczny, enkodowany w Base64.
* _cipherIv_ <= zawsze \
wektor inicjalizacji, enkodowany w Base64.

* _encryptionInfo_ <= zawsze \
struktura przygotowana do przekazania do metody KSeF.
  * _encryptedSymmetricKey_ <= zawsze \
klucz prywatny, zaszyfrowany kluczem publicznym serwera KSeF i enkodowany w Base64.
  * _initializationVector_ <= zawsze \
wektor inicjalizacji, enkodowany w Base64.

Przykład:
```json
{
	"cipherKey":"w9x5y0vA3eMLev9bmW7QBv52aY1ElsRgkI6wQh9riu8=",
	"cipherIv":"bNttLTOgmVzERqYjpimuXA==",
	"encryptionInfo":
	{
		"encryptedSymmetricKey":"cE5pXeMV2Rw3Xfm85JQoHrkeNLXDFTqMrN2xzgrY3wWm\/+Ud4UngAQ\/9XfCE2sYd7S\/LUw12NxCfNuaH\/a\/sA3Nki9HW+KGNxUHuqZH6J7j0S6I8BWCbcP7hQatW\/bP6W9LX+tVh+azjTQ9MxBEdEJhAjerumtldujoGLboiJxsYcHbUwIxVpeUVZX52hatBOfQWE198GSOxYQjY+2YOPEOjNxwy6Uxxe7IEM\/W0M0Ha8fwhuGspJqUUvnua46K7KHox02nJ5+JiIDC+0LqYQ8MglZR2rsai+yMTFZZ4hiuZlVzshQG0H1KBOa+D15LMVl16cS1+\/Z4ZoHbwAPWpPg==",
		"initializationVector":"bNttLTOgmVzERqYjpimuXA=="
	}
}
```
>[!TIP]
>Pola _cipherIv_ i _initializationVector_ są identyczne, ale pozostawiłem obydwa, aby było łatwiej dokonać ewentualnego podziału tej struktury na część "dla siebie" i "dla KSeF".

### #GetInvoiceQrLink
Zwraca url kodu QR faktury (w dokumentacji KSeF określany także jako "*kod QR I*") oraz, jeżeli wskażesz scieżkę do zapisania, tworzy odpowiedni plik obrazu (PNG) z tym kodem.

_[=> spis treści](#spis-tresci)_

ARGUMENTY: 
* _invoiceFile_ \
ścieżka do pliku faktury, dla której ma być stworzony ten link. Może być względna, np.:
```json
{"invoiceFile":"..\\FromPDF\\Result\\Faktura3.xml"}
```
* _invoiceBase64_ \
alternatywny sposób wskazania faktury: binarne dane z zawartością XML, enkodowane w Base64. UWAGA: *nie* należy zamieniać stringu XML na bajty, bo to może pomieszać jego enkodowanie. Raczej powinna to być np. zawartość pliku XML, wczytana jako dane binarne.
* _saveToPng_ \
ścieżka na obraz z kodem QR. Może być względna. Jeżeli podany w niej plik istnieje - zostanie nadpisany. Jeżeli nie zostanie podana, serwis wyznaczy tylko tekst linku, i zwróci go w rezultacie (element linkUrl).
* _imageSize_ \
długość boku obrazu, w pikselach. Domyślnie: 300. UWAGA: Wokół właściwego kodu QR program dodaje białą ramkę o szerokości 15%-10% podanego rozmiaru. (Im większy obraz, tym węższa ramka).
* _pixelsPerDot_ \
rozmiar elementarnego kwadratu w symbolu QR (w pikselach). Domyślnie: 20. W związku z tym, że złożony z takich elementarnych kwadratów obraz jest zmniejszany, aby dopasować się do _imageSize_, ta wartość wpływa jedynie na ostrość krawędzi pól w uzyskanym kodzie QR.

UWAGI:
* _invoiceFile_ i _invoiceBase64_ to alternatyw - jedna z nich musi być zawsze podana.
* Jeżeli nie podano _saveToPng_, program zwróci tylko tekst w polu _linkUrl_. Nie będzie wyznaczał obrazu.
* Wartości _imageSize_ i _pixelsPerDot_ mają wartości domyślne i nie trzeba ich podawać nawet gdy określiłeś _saveToPng_.

REZULTAT:
* _linkUrl_ <= zawsze \
Link, który jest zakodowany w kodzie QR.
* _pathToPng_ \
Pełna ścieżka do pliku z obrazem. To proste rozwinięcie argumentu _saveToPng_. Nie istnieje, gdy ten nie został podany.

Przykład:
```json
{
    "linkUrl":"https://ksef-test.mf.gov.pl/client-app/invoice/5251469286/13-04-2023/Ud771AQuFnocLx1ZpW8lGdtVy7cpynzfmzsA_uNMK6w",
    "pathToPng":"C:\\Users\\Hyperbook\\source\\repos\\KSeF-API\\KSeF.Services\\bin\\Debug\\certs\\InvoiceQR.png"
}
```
UWAGI: 
* Na razie kody QR otrzymywane z biblioteki .NET MF są rozmyte, w wyniku skalowania oryginalnego obrazu stworzonego przez komponent QRCoder. Aby to obejść, generuję tylko teksty linków, które przekazuje do mojego własnego generatora QR (Stamper), który nanosi je na faktury.


### #GetCertificateQrLink
Zwraca url kodu QR certyfikatu, który ma być nanoszony na wizualizację faktur wystawionych offline. (W dokumentacji KSeF określany także jako "*kod QR II*"). Jeżeli wskażesz scieżkę do zapisania, stworzy odpowiedni plik obrazu (PNG) z tym kodem.

_[=> spis treści](#spis-tresci)_

ARGUMENTY: 
* _invoiceFile_ \
ścieżka do pliku faktury, dla której ma być stworzony ten link. Może być względna.
* _invoiceBase64_ \
alternatywny sposób wskazania faktury: binarne dane z zawartością XML, enkodowane w Base64. UWAGA: *nie* należy zamieniać stringu XML na bajty, bo to może pomieszać jego enkodowanie. Raczej powinna to być np. zawartość pliku XML, wczytana jako dane binarne.
* _certificateFile_ \
ścieżka do pliku z certyfikatem w formacie PEM.
* _certificatePem_ \
tekst certyfikatu PEM:
```
"-----BEGIN CERTIFICATE----- 
... 
-----END CERTIFICATE-----"
```
* _privateKeyFile_ \
ścieżka do pliku z kluczem prywatnym, w formacie PEM.
* _privateKeyPem_ \
tekst klucza prywatnego PEM:
```
"-----BEGIN PRIVATE KEY-----
...
-----END PRIVATE KEY-----"
```
* _certificateSerial_ \
Użyj tego parametru tylko wtedy, gdy numer seryjny wpisany w certyfikacie różni z jakiegoś powodu od numeru seryjnego w KSeF. (Pole dodane wyłącznie "na wszelki wypadek").
* _contextNip_ \
Użyj tego parametru wtedy, gdy certyfikat nie pochodzi od pieczęci firmowej. (Tzn. w polu Subject nie ma sekcji OID.2.5.4.97 z numerem NIP przedsiębiorstwa). 
* _contextIid_ \
Identyfikator wewnętrzny (w postaci *\<NIP\>-XXXXX*). Użyj tego parametru tylko wtedy, gdy certyfikat jest związany z tzw. "wydzieloną częścią przedsiębiorstwa", sygnującą się takim identyfikatorem wewnętrznym.
* _contextNipVatUe_ \
Numer rejestracyjny VAT spoza Polski. Użyj tego parametru tylko wtedy, gdy certyfikat jest związany z takim podmiotem.
* _saveToPng_ \
ścieżka na obraz z kodem QR. Może być względna. Jeżeli podany w niej plik istnieje - zostanie nadpisany. Jeżeli nie zostanie podana, serwis wyznaczy tylko tekst linku, i zwróci go w rezultacie (element *linkUrl*).
* _imageSize_ \
długość boku obrazu, w pikselach. Domyślnie: 480. UWAGA: Wokół właściwego kodu QR program dodaje białą ramkę o szerokości 15%-10% podanego rozmiaru. (Im większy obraz, tym węższa ramka).
* _pixelsPerDot_ \
rozmiar elementarnego kwadratu w symbolu QR (w pikselach). Domyślnie: 20. W związku z tym, że złożony z takich elementarnych kwadratów obraz jest zmniejszany, aby dopasować się do _imageSize_, ta wartość wpływa jedynie na ostrość krawędzi pól w uzyskanym kodzie QR.

Przykład:
```json
{
    "invoiceFile":"C:/Users/Hyperbook/Documents/ZigZak/KSeF/FromPDF/Result/Faktura3.xml",
    "certificateFile":"../certs/certificate.pem",
    "privateKeyFile":"../certs/privatekey.pem"
}
```
lub:
```json
{
    "invoiceFile":"C:/Users/Hyperbook/Documents/ZigZak/KSeF/FromPDF/Result/Faktura3.xml",
    "certificateFile":"../certs/certificate.pem",
    "privateKeyFile":"../certs/privatekey.pem",
    "saveToPng":"../../certs/CertificateQR.png",
	"imageSize":490
}
```
UWAGI:
* _invoiceFile_ i _invoiceBase64_ to alternatywa - jedna z nich musi być zawsze podana.
* Certyfikaty i klucz prywatny w formacie PEM mogą być podane wprost, jako tekst (para _CertificatePem_ + _PrivateKeyPem_) lub poprzez wskazanie zawierających je plików (para _CertificateFile_ + _PrivateKeyFile_).
* Jeżeli "_privateKeyPem/File_" jest pominięty, program zakłada, że klucz prywatny znajduje się także w tekście "_certificatePem/File_"
* Należy podać wartość "_certificatePem/File_"[+"_privateKeyPem/File_"] LUB "_certificateName_". To alternatywa. Brak jakiejkolwiek informacji o certyfikacie wywoła wyjątek. 
* Jeżeli nie podano _saveToPng_, program zwróci tylko tekst w polu _linkUrl_. Nie będzie wyznaczał obrazu.
* Wartości _imageSize_ i _pixelsPerDot_ mają wartości domyślne i nie trzeba ich podawać nawet gdy określiłeś _saveToPng_.

REZULTAT:
* _linkUrl_ <= zawsze \
Link, który jest zakodowany w kodzie QR.
* _pathToPng_ \
Pełna ścieżka do pliku z obrazem. To proste rozwinięcie argumentu _saveToPng_. Nie istnieje, gdy ten nie został podany.

Przykład:
```json
{
    "linkUrl":"https://ksef-test.mf.gov.pl/client-app/invoice/5251469286/13-04-2023/Ud771AQuFnocLx1ZpW8lGdtVy7cpynzfmzsA_uNMK6w",
    "pathToPng":"C:\\Users\\Hyperbook\\source\\repos\\KSeF-API\\KSeF.Services\\bin\\Debug\\certs\\InvoiceQR.png"
}
```
UWAGI: 
* Nie zaimplementowano udostępnionej w bibliotece .NET funkcji umieszczania etykiet z numerem KSEF lub słowami "OFFLINE", "CERTYFIKAT" pod obrazkiem. 
* Na razie kody QR otrzymywane z biblioteki .NET są rozmyte, w wyniku nieumiejętnego obchodzenia się jej autorów z obrazem. 

### #CreateTestSeal

Tworzy testowy (*self-signed*) certyfikat osoby prawnej (tzw. pieczęć firmową) i zapisuje go na dysku, wraz z kluczem prywatnym.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* *formalName* <= zawsze  \
  Oficjalna nazwa podmiotu (firmy). Na przykład: "Test sp. z o.o.".
* *nip*  <= zawsze \
  Numer NIP podmiotu. 10 cyfr, to dane testowe - możesz wpisać cokolwiek
* *commonName*  
  Potoczna nazwa certyfikowanego podmiotu. Na przykład: "Test". Jeżeli jej nie podasz - program wpisze tu *formalName*.
* *validFrom*  
  od kiedy ten certyfikat jest ważny ("rrrr-MM-dd\[ hh:mm:ss]"). Jeżeli nie podasz tej daty - przjęta jest aktualny czas (UTC).
* *validTo*  
  termin ważności certyfikatu ("rrrr-MM-dd\[ hh:mm:ss]"). Jeżeli nie podasz tej daty - przyjęty będzie okres 1 roku.
* *encryption*  
  typ zastosowanego klucza. Domyślnie to "RSA", ale możesz też wpisać "ECDsa" (tzw. "klucz eliptyczny"). Wpisuj te symbole zachowując podaną tu konwencję dużych i małych liter!
* *saveTo*  <= zawsze \
  ścieżka do pliku, w którym ma zostać zapisany utworzony certyfikat wraz z kluczem prywatnym. Na przykład: "*../certs/seal.pem*". Rozszerzenie nazwy pliku określa format, w którym certyfikat ma być zapisany. Możliwe są dwa: ".pem" lub ".pfx". Jeżeli podasz jakikolwiek inny, program zapisze wynik do pliku *\*.pem*.

Przykład:

```json
{ 
	"formalName"  : "Test sp. z o.o.", 
	"nip" : "1234567890",  
	"saveTo" : "../certs/seal.pem" 
}
```

lub:

```json
{ 
	"formalName"  : "Test sp. z o.o.", 
	"commonName" : "Test",  
	"nip" : "1234567890",  
	"validFrom" : "2025-08-01",  
	"validTo" : "2025-09-30",  
	"encryption" : "ECDsa",  
	"saveTo" : "../certs/seal-ecdsa.pfx" 
}
```

UWAGI:

* Nip musi być polski, nawet jeżeli kraj przedsiębiorstwa jest inny niż Polska (są takie przypadki).

REZULTAT:

* *certFile*  <= zawsze \
  Pełna ścieżka do pliku z certyfikatem. To proste rozwinięcie argumentu *saveTo*.

Przykład:

```json
{
	"certFile":"C:/Users/Hyperbook/source/repos/KSeF-API/KSeF.Services/bin/Debug/certs/seal-ecdsa.pfx"
}
```
>[!NOTE]
>Klucz prywatny w pliku wynikowym nie jest zabezpieczony żadnym hasłem. Gdyby np. pytał się o nie *openssl* lub inny program, należy zostawić puste pole i potwierdzić.

### #CreateTestSignature

Tworzy testowy (*self-signed*) certyfikat osoby fizycznej (czyli "podpis elektroniczny") i zapisuje go na dysku, wraz z kluczem prywatnym.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* *name* <= zawsze  
  Imię. Na przykład: "Jan".
* *surname* <= zawsze  
  Nazwisko. Na przykład: "Kowalski".
* *commonName*  
  Jeżeli nie podasz, program wpisze tu "*name* *surname*".
* *pesel*
  Numer PESEL osoby fizycznej. 11 cyfr, to dane testowe - możesz wpisać cokolwiek
* *nip*
  Numer NIP (osoby fizycznej). 10 cyfr, to dane testowe - możesz wpisać cokolwiek
* *validFrom*  
  od kiedy ten certyfikat jest ważny ("rrrr-MM-dd\[ hh:mm:ss]"). Jezeli nie podasz tej daty - przjęta jest aktualny czas (UTC).
* *validTo*  
  termin ważności certyfikatu ("rrrr-MM-dd\[ hh:mm:ss]"). Jezeli nie podasz tej daty - przyjęty będzie okres 1 roku.
* *encryption*  
  typ zastosowanego klucza. Domyślnie to "RSA", ale możesz też wpisać "ECDsa" (tzw. "klucz eliptyczny"). Wpisuj te symbole zachowując podaną tu konwencję dużych i małych liter!
* *saveTo*  <= zawsze
  ścieżka do pliku, w którym ma zostać zapisany utworzony certyfikat wraz z kluczem prywatnym. Na przykład: "../certs/signature.pem". Rozszerzenie nazwy pliku określa format, w którym certyfikat ma być zapisany. Możliwe są dwa: ".pem" lub ".pfx". Jeżeli podasz jakikolwiek inny, program zapisze wynik do pliku *\*.pem*.

Przykład:

```json
{ 
	"name"  : "Jan", 
	"surname"  : "Kowalski", 
	"pesel" : "1234567890123",  
	"saveTo" : "../certs/signature.pem" 
}
```

lub:

```json
{ 
	"name"  : "Jan", 
	"surname"  : "Kowalski", 
	"nip" : "1234567890",  
	"validFrom" : "2025-08-01",  
	"validTo" : "2025-09-30",  
	"encryption" : "ECDsa",  
	"saveTo" : "../certs/signature-ecdsa.pfx" 
}
```
UWAGI:

* Należy podać ALBO nip, albo pesel.

REZULTAT:

* *certFile*  <= zawsze \
  Pełna ścieżka do pliku z certyfikatem. To proste rozwinięcie argumentu *saveTo*.

Przykład:

```json
{
	"certFile":"C:/Users/Hyperbook/source/repos/KSeF-API/KSeF.Services/bin/Debug/certs/signature.pfx"
}
```
>[!NOTE]
>Klucz prywatny w pliku wynikowym nie jest zabezpieczony żadnym hasłem. Gdyby np. pytał się o nie *openssl* lub inny program, należy zostawić puste pole i potwierdzić.

### #ValidateKsefNumber
Weryfikuje numer KSeF.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _numberKSeF_ <= zawsze \
numer KSeF (string)
```json
	{"numberKSeF":"7781464139-20230721-80B5109675F7-B5"}
```

REZULTAT:
* _result_ <= zawsze \
Symbol rezultatu - jedna z wartości: "ok" | "emptyString", | "wrongLength" | "badCrc".

* _crc_ \
Gdy _result_ = "badCrc", podaje poprawną wartość sumy kontrolnej.

Przykład:

```json
	{ 
		"result":"badCrc", 
		"crc":"2F"
	}
```
### #GetTokenProperties
Zwraca metadane podanego tokena dostępowego JWT. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _accessToken_ <= zawsze \
Analizowany token dostępowy.

Przykład:
```json
{
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

Strukturę opisującą właściwości tokena. Na przykład dla użytkownika z podmiotu o Nip:5251469286, pracującego w kontekście Nip:7781464139:

```json
{
    "issuer": "ksef-api-te",
    "audiences": [ "ksef-api-te" ],
    "issuedAt": "2025-10-24T07:12:28+00:00",
    "expiresAt": "2025-10-24T07:27:28+00:00",
    "roles": [ "InvoiceRead", "InvoiceWrite" ],
    "tokenType": "ContextToken",
    "contextIdType": "Nip",
    "contextIdValue": "7781464139",
    "authMethod": "InternalCertificate",
    "authRequestNumber": "20251023-AU-37B12AE000-5252182A0A-92",
    "subjectDetails": 
    {
        "subjectIdentifier": { "type": "Nip", "value": "5251469286" },
        "givenNames": [],
        "commonName": "ZigZak",
        "countryName": "PL"
    },
    "permissions": [ "InvoiceRead", "InvoiceWrite" ],
    "permissionsExcluded": [],
    "rolesRaw": [],
    "permissionsEffective": []
}
```
(Nip:7781464139 udzielił wcześniej Nip:5251469286 uprawnień _InvoiceRead_, _InvoiceWrite_, dzięki czemu Nip:5251469286 może się zalogowac do jego kontekstu).

Własciwości tokena gdy użytkownik z Nip:5251469286 jest "u siebie", zalogowany pieczęcią firmową, wyglądają tak:

```json
{
    "issuer": "ksef-api-te",
    "audiences": [ "ksef-api-te" ],
    "issuedAt": "2025-10-24T07:04:17+00:00",
    "expiresAt": "2025-10-24T07:19:17+00:00",
    "roles": [ "Owner" ],
    "tokenType": "ContextToken",
    "contextIdType": "Nip",
    "contextIdValue": "5251469286",
    "authMethod": "InternalCertificate",
    "authRequestNumber": "20251023-AU-2BAE5F5000-21574403FA-58",
    "subjectDetails": 
    {
        "subjectIdentifier": { "type": "Nip", "value": "5251469286" },
        "givenNames": [ ],
        "commonName": "ZigZak",
        "countryName": "PL"
    },
    "permissions": [ "Owner" ],
    "permissionsExcluded": [],
    "rolesRaw": [],
    "permissionsEffective": [ ]
}
```

Własciwości tokena tego samego użytkownika po uwierzytelnieniu osobistym certyfikatem kwalifikowanym (podpisem), wyglądają tak:
```json
{
    "issuer": "ksef-api-te",
    "audiences": [ "ksef-api-te" ],
    "issuedAt": "2025-10-29T11:42:26+00:00",
    "expiresAt": "2025-10-29T11:57:26+00:00",
    "roles": [ "Owner" ],
    "tokenType": "ContextToken",
    "contextIdType": "Nip",
    "contextIdValue": "5251469286",
    "authMethod": "QualifiedSignature",
    "authRequestNumber": "20251029-AU-2831CD6000-6CC30B81DB-87",
    "subjectDetails": 
    { 
	    "subjectIdentifier": { "type": "Pesel", "value": "@@@@@@@@@@9" },
        "givenNames": [],
        "surname": "Jaworski",
        "serialNumber": "PNOPL-@@@@@@@@@@9",
        "commonName": "Witold Jaworski",
        "countryName": "PL"
    },
    "permissions": [ "Owner" ],
    "permissionsExcluded": [],
    "rolesRaw": [],
    "permissionsEffective": []
}
```
(Numer PESEL zamaskowałem "@@@@@@@@@@")

### #CertificateToPem
Odczytuje certyfikat ze wskazanego pliku i zwraca go w postaci PEM z niezaszyfrowanym kluczem prywatnym (PKCS8)

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _certificateFile_ \
ścieżka do pliku z certyfikatem. Na przykład: "*../certs/seal.pem*". Obsługiwane formaty: ".crt", ".pem" lub ".pfx" i ".p12". Jeżeli pominiesz ten argument, musisz podać _certificatePem_

* _certificatePem_ \
tekst certyfikatu PEM (np. z zaszyfrowanym kluczem prywatnym):
```
"-----BEGIN CERTIFICATE----- 
... 
-----END ENCRYPTED PRIVATE KEY-----"
```
* _privateKeyFile_ \
ścieżka do ewentualnego pliku z kluczem prywatnym, w formacie PEM. (Np. o rozszerzeniu *.key).

* _privateKeyPem_ \
tekst klucza prywatnego PEM, który może być zaszyfrowany :
```
"-----BEGIN ENCRYPTED PRIVATE KEY-----
...
-----END ENCRYPTED PRIVATE KEY-----"
```
* _password_ \
ewentualne hasło do odszyfrowania klucza prywatnego certyfikatu.

Na przykład:

```json
{
	"certificateFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.crt",
	"privateKeyFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.key",
	"password":"alamakota#19300407bydgoszcz"
}
```
REZULTAT:

* _certificatePem_ <= zawsze\
tekst wynikowego certyfikatu PEM.

Na przykład:

```json
{
	"certificatePem":"-----BEGIN CERTIFICATE----- ... -----END PRIVATE KEY-----"
}
```


### #CertificateFromPem
Zapisuje certyfikat podany w postaci PEM z niezaszyfrowanym kluczem prywatnym (PKCS8) do wskazanego pliku/plików. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _certificatePem_ <= zawsze\
tekst certyfikatu PEM:
```
"-----BEGIN CERTIFICATE----- 
... 
-----END PRIVATE KEY-----"
```
* _password_ \
ewentualne hasło do zaszyfrowania klucza prywatnego PEM.

* _certificateFile_ \
ścieżka do pliku z certyfikatem w formacie PEM.

* *saveTo*  \
  ścieżka do pliku, w którym ma zostać zapisany utworzony certyfikat wraz z kluczem prywatnym. Na przykład: "*../certs/seal.pem*". Rozszerzenie nazwy pliku określa format, w którym certyfikat ma być zapisany. Możliwe są: ".crt", ".pem" lub ".pfx" i ".p12". Jeżeli pominiesz ten argument, żądanie zwróci certyfikatu w postaci tekstu PEM, po ewentualnym zaszyfrowaniu klucza prywatnego (gdy _password_ będzie niepuste).

* _privateKeyFile_ \
ewentualna ścieżka do oddzielnego pliku z kluczem prywatnym, w formacie PEM (jeżeli ma być zapisany oddzielnie). Ignorowany, gdy _saveTo_ nie został podany, lub gdy ma rozszerzenia ".pfx" lub ".p12".

```json
{
	"certificatePem":"-----BEGIN CERTIFICATE----- ... -----END PRIVATE KEY-----",
	"saveTo":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.pfx",
	"password":"alamakota"
}
```

albo:
```json
{
	"certificatePem":"-----BEGIN CERTIFICATE----- ... -----END PRIVATE KEY-----",
	"saveTo":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.crt",
	"password":"alamakota"
}
```
>[!NOTE]
>Jeżeli pominąłeś argument _privateKeyFile_, dla plików ".crt" zostanie stworzony plik z kluczem prywatnym o takiej samej nazwie, ale rozszerzeniu ".key".

REZULTAT:

* _certificateFile_ \
ścieżka do pliku z certyfikatem (rozwinięcie _saveTo_).
* _privateKeyFile_ \
ewentualna ścieżka do pliku z kluczem prywatnym, w formacie PEM. To rozwinięcie argumentu żądania (o ile go podałeś). Gdy pominąłeś go w argumentach, to jest automatycznie tworzony dla certyfikatów zapisywanych do plików ".crt".
* _certificatePem_ \
tekst wynikowego certyfikatu PEM. Zostanie zwrócony tylko wtedy, gdy w argumentach pominąłeś _saveTo_.

Przykłady:
```json
{
	"certificateFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.pfx",
}
```

albo

```json
{
	"certificateFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.crt",
	"privateKeyFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\test.key"
}
```

### #GetCertificateProperties
Zwraca metadane certyfikatu. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

Należy podać **jeden** z wymienionych poniżej opcjonalnych argumentów:

* _certificateFile_ \
ścieżka do pliku z certyfikatem w formacie PEM lub PFX.

* _certificatePem_ \
tekst certyfikatu PEM:
```
"-----BEGIN CERTIFICATE----- 
... 
-----END CERTIFICATE-----"
```
* _privateKeyFile_ \
ewentualna ścieżka do pliku z kluczem prywatnym, w formacie PEM (o ile nie ma go w certyfikacie).

* _privateKeyPem_ \
ewentualny tekst klucza prywatnego PEM (o ile nie ma go w certyfikacie):
```
"-----BEGIN PRIVATE KEY-----
...
-----END PRIVATE KEY-----"
```
* _password_ \
ewentualne hasło do odszyfrowania klucza prywatnego certyfikatu.

* _signedXmlFile_ \
ścieżka do podpisanego pliku XML (zwraca metadane certyfikatu użytego do podpisu).

* _signedXml_ \
tekst podpisanego XML (zwraca metadane certyfikatu użytego do podpisu).

>[!NOTE]
>Jeżeli XML przekazany do analizy zawiera wiele podpisów, program zwraca informacje o certyfikacie **pierwszego**.

Przykład:

```json
{
	"signedXmlFile":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\Request.ePUAP.xml"
}
```

REZULTAT:

Żądanie zwraca strukturę opisującą certyfikat. Na przykład:

```json
{
  "name": "WITOLD JAWORSKI",
  "serialNumber": "00EFD46F5B31B845D08071C5EED94D4EB9D37D",
  "validFrom": "2024-02-13",
  "validTo": "2027-02-12",
  "subject": 
  {
    "CN": "Minister do spraw informatyzacji - pieczęć podpisu zaufanego",
    "O": "Ministerstwo Cyfryzacji",
    "OID.2.5.4.97": "VATPL-5252955037",
    "C": "PL"
  },
  "issuer": 
  {
    "CN": "Centrum Kwalifikowane EuroCert",
    "O": "EuroCert Sp. z o.o.",
    "C": "PL",
    "OID.2.5.4.97": "VATPL-9512352379"
  },
  "publicKeyOid": "1.2.840.113549.1.1.1 (RSA)",
  "fingerprint": "4411A166427EF2E2ECCC63DF25E17F090FA64936",
  "hasPrivateKey": false,
  "extensions": 
  [
    {
      "critical": false,
      "oid": {
        "value": "2.5.29.35",
        "friendlyName": "Authority Key Identifier"
      },
      "rawData": "MBaAFHRicJn/G2A7xGS1hB+jFQxcy1+9"
    },
    {
      "critical": false,
      "oid": {
        "value": "2.5.29.14",
        "friendlyName": "Subject Key Identifier"
      },
      "rawData": "BBQolbJUsnSegzjinBFyi3728o9x4w=="
    },
    {
      "critical": true,
      "oid": {
        "value": "2.5.29.19",
        "friendlyName": "Basic Constraints"
      },
      "rawData": "MAA="
    },
    {
      "critical": true,
      "oid": 
	  {
        "value": "2.5.29.15",
        "friendlyName": "Key Usage"
      },
      "rawData": "AwIGwA=="
    },
    {
      "critical": false,
      "oid": 
	  {
        "value": "2.5.29.32",
        "friendlyName": "Certificate Policies"
      },
      "rawData": "MIICITCCAhIGCiqEaAGG+H8BAgMwggICMDAGCCsGAQUFBwIBFiRodHRwOi8vd3d3LmV1cm9jZXJ0LnBsL3JlcG96eXRvcml1bS8wggHMBggrBgEFBQcCAjCCAb4MggG6Q2VydHlmaWthdCB3eXN0YXdpb255IHpnb2RuaWUgeiBha3R1YWxueW0gZG9rdW1lbnRlbTogUG9saXR5a2EgQ2VydHlmaWthY2ppIGkgS29kZWtzIFBvc3RlcG93YW5pYSBDZXJ0eWZpa2FjeWpuZWdvIEt3YWxpZmlrb3dhbnljaCBVc2x1ZyBaYXVmYW5pYSBFdXJvQ2VydCwgem5hamR1amFjeW0gc2llIHcgcmVwb3p5dG9yaXVtOiBodHRwOi8vd3d3LmV1cm9jZXJ0LnBsL3JlcG96eXRvcml1bS8uIFRoaXMgY2VydGlmaWNhdGUgd2FzIGlzc3VlZCBhY2NvcmRpbmcgdG8gdGhlIGN1cnJlbnQgZG9jdW1lbnQ6IENlcnRpZmljYXRlIFBvbGljeSBhbmQgQ2VydGlmaWNhdGlvbiBQcmFjdGljZSBTdGF0ZW1lbnQgb2YgRXVyb0NlcnQgUXVhbGlmaWVkIFRydXN0IFNlcnZpY2VzLCB3aGljaCBjYW4gYmUgZm91bmQgYXQgaHR0cDovL3d3dy5ldXJvY2VydC5wbC9yZXBvenl0b3JpdW0vLjAJBgcEAIvsQAEB"
    },
    {
      "critical": false,
      "oid": 
	  {
        "value": "2.5.29.31",
        "friendlyName": "CRL Distribution Points"
      },
      "rawData": "MCgwJqAkoCKGIGh0dHA6Ly9jcmwuZXVyb2NlcnQucGwvcWNhMDMuY3Js"
    },
    {
      "critical": false,
      "oid": 
	  {
        "value": "1.3.6.1.5.5.7.1.1",
        "friendlyName": "Authority Information Access"
      },
      "rawData": "MEMwQQYIKwYBBQUHMAKGNWh0dHBzOi8vZXVyb2NlcnQucGwvcHViL1ByYXdvL1FDQTAzX0V1cm9jZXJ0XzIwMTcuZGVy"
    },
    {
      "critical": false,
      "oid": 
	  {
        "value": "1.3.6.1.5.5.7.1.3",
        "friendlyName": "Qualified Certificate Statements"
      },
      "rawData": "MAowCAYGBACORgEB"
    }
  ]
}
```
A tak wygląda certyfikat KSeF z testowej pieczęci firmowej:
```json
{
    "name": "ZigZak (uwierzytelnienie)",
    "serialNumber": "018B825ED6DF7913",
    "validFrom": "2025-10-17",
    "validTo": "2027-10-17",
    "subject": {
        "CN": "ZigZak (uwierzytelnienie)",
        "O": "ZigZak Witold Jaworski",
        "OID.2.5.4.97": "VATPL-5251469286",
        "C": "PL"
    },
    "issuer": {
        "CN": "TEST CCK KSeF",
        "OU": "Krajowa Administracja Skarbowa",
        "O": "Ministerstwo Finansów",
        "C": "PL"
    },
    "publicKeyOid": "1.2.840.113549.1.1.1 (RSA)",
    "fingerprint": "B0B3DA99D382A8767E2BCE7A159413E41D40BEB2",
    "hasPrivateKey": false,
    "extensions": [
        {
            "critical": false,
            "oid": {
                "value": "2.5.29.35",
                "friendlyName": "Authority Key Identifier"
            },
            "rawData": "MBaAFDo6gRAfqLQ33sGDY/s5iPqJpCUy"
        },
        {
            "critical": true,
            "oid": {
                "value": "2.5.29.19",
                "friendlyName": "Basic Constraints"
            },
            "rawData": "MAA="
        },
        {
            "critical": true,
            "oid": {
                "value": "2.5.29.15",
                "friendlyName": "Key Usage"
            },
            "rawData": "AwIHgA=="
        },
        {
            "critical": false,
            "oid": {
                "value": "2.5.29.14",
                "friendlyName": "Subject Key Identifier"
            },
            "rawData": "BBQTZHEeUC5KSrQw5otrStj/HXAhFg=="
        },
        {
            "critical": false,
            "oid": {
                "value": "2.5.29.31",
                "friendlyName": "CRL Distribution Points"
            },
            "rawData": "MEAwPqA8oDqGOGh0dHBzOi8va3NlZi10ZXN0Lm1mLmdvdi5wbC9zZWN1cml0eS9jcmwvdGVzdG1ma3NlZjIuY3Js"
        },
        {
            "critical": false,
            "oid": {
                "value": "1.3.6.1.5.5.7.1.3",
                "friendlyName": "Qualified Certificate Statements"
            },
            "rawData": "MCIwCwYGBACORgEDAgEAMBMGBgQAjkYBBjAJBgcEAI5GAQYC"
        }
    ]
}
```


### #ListStoreCertificates
Zwraca listę metadanych aktywnych certyfikatów z wybranego magazynu certyfikatów Windows.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _store_ <= zawsze  \
  Lokalizacja magazynu. Zazwyczaj to "CurrentUser" - jego ekwiwalentem jest także wartośc "". Alternatywa to "LocalMachine" (dodałem tę możliwość na wszelki wypadek). Inne możliwe lokalizacje powstają, gdy w Windows jest uruchomiona _Certification Service_. Pozostawiłem możliwość ich wpisania, ale nie sądzę, bym kiedykolwiek z nich miał korzystać.

* _oidPolicies_ \
Filtr: rozdzielana średnikiem lista kodów OID polityk certyfikatu. Metoda zwraca certyfikat, w którym wystąpił jeden z tych OID. Wartość domyślna to kody OID występujące w certyfikatach kwalifikowanych: ```"0.4.0.194112.1.1;0.4.0.194112.1.2;0.4.0.194112.1.3"``` \
Jeżeli chcesz wyłączyć ten filtr - nadaj mu wartość "".

* _subjectPhrases_ \
Filtr: rozdzielana średnikiem lista tekstów. Metoda zwraca certyfikat, w którego polu _subject_ wystąpiła jedna z tych fraz. Domyślnie ten filtr jest wyłączony - ma wartość "".
>[!TIP]
>Możesz nadac _subjectPhrases_ wartości _"PNOPL-;TIN-;NIP-;VATPL-"_, aby uzyskać tylko te certyfikaty, którymi możesz uwierzytelnić się w KSeF.

* _usedFor_ \
  Filtr na deklarowane zastosowania certyfikatu. Wartością może być jeden z symboli, lub jego wartość liczbowa, podana w nawiasie z prawej strony:
	*  _"None"_ (0)
	*  _"EncipherOnly"_ (1)
	*  _"CrlSign"_ (2)
	*  _"KeyCertSign"_ (4)
	*  _"KeyAgreement"_ (8)
	*  _"DataEncipherment"_ (16)
	*  _"KeyEncipherment"_ (32)
	*  _"NonRepudiation"_ (64)
	*  _"DigitalSignature"_ (128)
	*  _"DecipherOnly"_ (32768)

	Jeżeli chcesz wybrać tylko certyfikaty posiadające jednocześnie więcej niż jedno określone zastosowanie - podaj jako wartość sumę ich wartości. Domyślnie wartość tego pola to suma _"DigitalSignature"_ + _"NonRepudiation"_.

Przykład:

```json
{"store":""}
```
zwraca znalezione w magazynie użytkownika certyfikaty kwalifikowane służące do podpisu i logowania.

REZULTAT:

Zwraca listę struktur opisujących certyfikaty odpowiadające kryterium podanym w argumentach żądania. Na przykład:

```json
[
{
  "name": "Witold Jaworski",
  "serialNumber": "47CA95B5C2AECDBD1C05DEAA74243346",
  "validFrom": "2023-10-18",
  "validTo": "2026-10-31",
  "subject": 
  {
    "C": "PL",
    "SERIALNUMBER": "PNOPL-??????????9",
    "SN": "Jaworski",
    "G": "Witold",
    "CN": "Witold Jaworski"
  },
  "issuer": 
  {
        "OID.2.5.4.97": "VATPL-5170359458",
        "CN": "Certum QCA 2017",
        "O": "Asseco Data Systems S.A.",
        "C": "PL"
  },
  "publicKeyOid": "1.2.840.113549.1.1.1 (RSA)",
  "fingerprint": "01A22C777F571FCE9E93A1A667A787CE7EA45497",
  "hasPrivateKey": false,
  "extensions": 
  [
    {
      "critical": true,
      "oid": 
      {
        "value": "2.5.29.19",
        "friendlyName": "Basic Constraints"
      },
      "rawData": "MAA="
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "2.5.29.31",
        "friendlyName": "CRL Distribution Points"
      },
      "rawData": "MC0wK6ApoCeGJWh0dHA6Ly9xY2EuY3JsLmNlcnR1bS5wbC9xY2FfMjAxNy5jcmw="
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "1.3.6.1.5.5.7.1.1",
        "friendlyName": "Authority Information Access"
      },
      "rawData": "MGQwLAYIKwYBBQUHMAGGIGh0dHA6Ly9xY2EtMjAxNy5xb2NzcC1jZXJ0dW0uY29tMDQGCCsGAQUFBzAChihodHRwOi8vcmVwb3NpdG9yeS5jZXJ0dW0ucGwvcWNhXzIwMTcuY2Vy"
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "2.5.29.35",
        "friendlyName": "Authority Key Identifier"
      },
      "rawData": "MBaAFCfx2E5gUGi2Yf5oGyhsbeQLcwlN"
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "2.5.29.14",
        "friendlyName": "Subject Key Identifier"
      },
      "rawData": "BBTjYpPCibjVuhrMTkdhmbPeBz5Kdw=="
    },
    {
      "critical": true,
      "oid": 
      {
        "value": "2.5.29.15",
        "friendlyName": "Key Usage"
      },
      "rawData": "AwIGwA=="
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "2.5.29.32",
        "friendlyName": "Certificate Policies"
      },
      "rawData": "MEwwCQYHBACL7EABAjA/BgwqhGgBhvZ3AgQBDAEwLzAtBggrBgEFBQcCARYhaHR0cDovL3d3dy5jZXJ0dW0ucGwvcmVwb3p5dG9yaXVt"
    },
    {
      "critical": false,
      "oid": 
      {
        "value": "1.3.6.1.5.5.7.1.3",
        "friendlyName": "Qualified Certificate Statements"
      },
      "rawData": "MIGyMAgGBgQAjkYBATAIBgYEAI5GAQQwgYYGBgQAjkYBBTB8MDwWNmh0dHBzOi8vcmVwb3NpdG9yeS5jZXJ0dW0ucGwvUERTL0NlcnR1bV9RQ0EtUERTX0VOLnBkZhMCZW4wPBY2aHR0cHM6Ly9yZXBvc2l0b3J5LmNlcnR1bS5wbC9QRFMvQ2VydHVtX1FDQS1QRFNfUEwucGRmEwJwbDATBgYEAI5GAQYwCQYHBACORgEGAQ=="
    }
  ]
}
]
```
Pole _name_ może nie występować, gdy tzw. _FriendlyName_ certyfikatu jest puste.


### #CreateAuthRequest
Tworzy i wypełnia danymi strukturę XML z żądaniem autoryzacji użytkownika.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _challenge_ <= zawsze  \
  Wartość uzyskana z [GetAuthChallenge](#getauthchallenge).

* _useFingertip_ \
  Użyj tej flagi z wartością **true** (bez cudzysłowia) gdy tworzona struktura będzie potem podpisywana certyfikatem osoby nie będącej obywatelem Polski. (Dokładniej: gdy na formularzu ZAW-FA jako identyfikator osoby fizycznej wskazano tzw. odcisk palca ("fingertip") jej certyfikatu). Domyślnie: false.

> Identyfikator tzw. "kontekstu", czyli podmiotu, do konta którego loguje się użytkownik (Należy zawsze podać jedno z poniższych pól):

* _nip_ \
  Numer NIP przedsiębiorstwa (np. "1234567890").

* _iid_ \
  Tzw. identyfikator wewnętrzny, nadany wydzielonej części przedsiębiorstwa / JST (to numer NIP + 5 znaków, np. "1234567890-00003")

* _nipVatUe_ \
  Numer NIP kontrahenta zagranicznego - np. "DE12345679833".

Przykład:
```json
{ 
  "challenge":"20250912-CR-226FB7B000-3ACF9BE4C0-10",
  "nip":"1234567890",
  "useFingertip":true
}
```

REZULTAT:

* _xmlContent_  <= zawsze \
  Tekst XML-a z wypełnioną strukturą żądania. Ten tekst można np. podpisać za pomocą [#XadesSign](#xadessign), a rezultat (_base64Content_) przekazać do [SubmitXadesAuthRequest](#submitxadesautheequest) jako wartość pola _signedRequestBase64_.

### #XadesSign
Podpisuje wskazany plik XML lub jego zawartość (podaną jako tekst) podpisem XAdES.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _srcFile_ \
ścieżka do pliku _*.xml_, który ma być podpisany
* _xmlContent_ \
Tekst XML, który ma być podpisany
* _dstFile_ \
ścieżka do wynikowego (podpisanego) pliku _*.xml_. Jeżeli taki plik już istnieje - będzie nadpisany.
* _certificateFile_ \
ścieżka do pliku z certyfikatem w formacie PEM lub PFX.
* _certificatePem_ \
tekst certyfikatu PEM:
```
"-----BEGIN CERTIFICATE----- 
... 
-----END CERTIFICATE-----"
```
* _privateKeyFile_ \
ścieżka do pliku z kluczem prywatnym, w formacie PEM.
* _privateKeyPem_ \
tekst klucza prywatnego PEM:
```
"-----BEGIN PRIVATE KEY-----
...
-----END PRIVATE KEY-----"
```
* _password_ \
ewentualne hasło do odszyfrowania klucza prywatnego certyfikatu.

* * _certificateName_ \
Pobiera certyfikat o podanej nazwie (tzw. _"Friendly Name"_) i jego klucz prywatny z magazynu certyfikatów Windows aktualnego użytkownika. 
* _certificateSn_ \
Pobiera certyfikat o podanym numerze seryjnym i jego klucz prywatny z magazynu certyfikatów Windows aktualnego użytkownika. 
>[!NOTE]
>Podczas wyszukiwania w magazynie Windows program pomja certyfikaty przeterminowane. Może wyświetlić systemowe okno dialogowe do wpisania PIN certyfikatu kwalifikowanego (np. takiego z karty). 

Przykładowa struktura argumentów odczytująca certyfikat z plików _*.pem_:
```json
{ 
	"srcFile"  : "../Request.xml", 
	"dstFile" : "../RequestSigned.xml",
	"certificateFile" : "../certificate.pem", 
	"privateKeyFile" : "../privatekey.pem"  
}
```
Przykładowa struktura argumentów pobierająca certyfikat o nazwie 'Witold Jaworski' z magazynu certyfikatów Windows (może to być np. certyfikat kwalifikowany z karty):
```json
{ 
	"srcFile"  : "../Request.xml", 
	"dstFile" : "../RequestSigned.xml",
	"certificateName" : "Witold Jaworski"  
}
```
REZULTAT:
* _signedFile_ \
Ścieżka (bezwzględna) do podpisanego pliku XML (to rozwinięcie podanego w argumentach _dstFile_).
* _base64Content_ \
podpisany XML jako dane binarne, enkodowane w Base64 (zwracany, gdy w argumentach nie podano _dstFile_).
```json
{"signedFile":"C:\\Users\\Hyperbook\\source\\repos\\KSeF-API\\KSeF.Services\\bin\\Debug\\RequestSigned.xml"}
```
UWAGI: 
* XML do podpisania można wskazać jako plik (_srcFile_) lub jego zawartosć już odczytana jako tekst (_xmlContent_). Jeden z tych dwóch argumentów musi zostać podany.
* Certyfikaty i klucz prywatny w formacie PEM mogą być podane wprost, jako tekst (para _CertificatePem_ + _PrivateKeyPem_) lub poprzez wskazanie zawierających je plików (para _CertificateFile_ + _PrivateKeyFile_).
* Jeżeli "_privateKeyPem/File_" jest pominięty, program zakłada, że klucz prywatny znajduje się także w tekście "_certificatePem/File_"
* Należy podać wartość "_certificatePem/File_"[+"_privateKeyPem/File_"] LUB "_certificateName_". To alternatywa. Brak jakiejkolwiek informacji o certyfikacie wywoła wyjątek. 
* Jeżeli nie podano _dstFile_, to podpisany XML jest zwracany w "_base64Content_".


## Polecenia serwera API
Polecenia dostępne w trybie online (Gdy argument *TargetUrl* to adres jednego z serwerów: testowego, demo, produkcyjnego). Oprócz wyliczonych poniżej poleceń, w tym trybie sa także dostępne wszystkie [polecenia lokalne](#polecenia-lokalne).

### GetAuthChallenge
Pobiera chwilowy identyfikator ("challenge") żądania dostępu. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY: brak

REZULTAT:
* _challenge_ <= zawsze \
identyfikator (ważny przez 10 minut).

* _timestamp_ <= zawsze \
Czas wygenerowania identyfikatora (UTC!).

Przykład:

```json
{ 
	"challenge":"20250912-CR-226FB7B000-3ACF9BE4C0-10", 
	"timestamp":"2025-09-12T12:23:56.015+00:00"
}
```
### SubmitXadesAuthRequest
Wysyła żądanie autoryzacji użytkownika.

_[=> spis treści](#spis-tresci)_

ARGUMENTY: (Wariant 1: wewnętrznie tworzy i podpisuje XML żądania)

* _challenge_ <= zawsze  \
  Wartość uzyskana z [GetAuthChallenge](#getauthchallenge).

> Identyfikator tzw. "kontekstu", czyli podmiotu, do konta którego loguje się użytkownik (Należy zawsze podać jedno z trzech poniższych pól):

* _nip_ \
  Numer NIP przedsiębiorstwa (np. "1234567890").

* _iid_ \
  Tzw. identyfikator wewnętrzny, nadany wydzielonej części przedsiębiorstwa / JST (to numer NIP + 5 znaków, np. "1234567890-00003")

* _nipVatUe_ \
  Numer NIP kontrahenta zagranicznego - np. "DE12345679833".

> Pola związane z certyfikatem KSeF użytkownika, który ma być użyty do uwierzytelnienia:

* _certificatePem_ <= zawsze \
  Certyfikat w formacie PEM (zazwyczaj - certyfikat KSeF)

* _privateKeyPem_ \
  Jeżeli _certificatePem_ nie zawiera klucza prywatnego - podaj go w tym parametrze (w formacie PEM).

* _useFingertip_ \
  Użyj tej flagi z wartością **true** (bez cudzysłowia) gdy _ksefCertificatePem_ należy do osoby nie będącej obywatelem Polski. (Dokładniej: gdy na formularzu ZAW-FA jako identyfikator osoby fizycznej wskazano tzw. odcisk palca ("fingertip") jej certyfikatu). Domyślnie: false.

* _verifyCertificationChain_ \
  Użyj tej flagi z wartością **true** (bez cudzysłowia) gdy chcesz zweryfikować cały "łańcuch uwierzytelnień" z _ksefCertificatePem_. To może znacznie wydłużyć czas logowania do KSeF. Domyślnie: false.

Przykład:
```json
{ 
  "challenge":"20250912-CR-226FB7B000-3ACF9BE4C0-10",
  "nip":"1234567890",
  "ksefCertificatePem":"-----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----",
  "ksefPrivateKeyPem":"-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----",
  "useFingertip":true
}
```
ARGUMENTY (Wariant 2: przekazujemy podpisany XML żądania):

* _signedRequestFile_ \
  Ścieżka do podpisanego pliku żądania autoryzacji. (Do podpisania można użyć np. polecenia [#XadesSign](#xadessign)).

* _signedRequestBase64_ \
  Podpisany XML jako dane binarne, enkodowane w Base64. (Można tu podstawić być wartość _base64Content_, zwróconą przez [#XadesSign](#xadessign)).

* _verifyCertificationChain_ \
  Użyj tej flagi z wartością **true** (bez cudzysłowia) gdy chcesz zweryfikować cały "łańcuch uwierzytelnień" z _ksefCertificatePem_. To może znacznie wydłużyć czas logowania do KSeF. Domyślnie: false.
>[!NOTE]
> Podpisany XML musi być podany: albo jako _signedRequestFile_, albo jako _signedRequestBase64_. Inaczej żądanie zgłosi brak danych wejściowych.

Przykładowa zawartość pliku żądania <u>PRZED</u> podpisaniem:

```xml
<?xml version="1.0" encoding="utf-8"?>
<AuthTokenRequest xmlns="http://ksef.mf.gov.pl/auth/token/2.0">
   <Challenge>20250604-CR-461EA5B000-537A6BA15D-D7</Challenge>
   <ContextIdentifier>
   	<Nip>1234563218</Nip>
   </ContextIdentifier>
   <SubjectIdentifierType>certificateSubject</SubjectIdentifierType>
</AuthTokenRequest>
```

Wyjaśnienie znaczenia pól tego XML znajduje się [tutaj](https://github.com/CIRFMF/ksef-docs/blob/main/uwierzytelnianie.md#1-przygotowanie-dokumentu-xml-authtokenrequest), a jego schemat XSD - [tutaj](https://ksef-test.mf.gov.pl/docs/v2/schemas/authv2.xsd).

Podpis powinien zostać dodany jako ostatni element tej struktury. Przykład:

```xml
<?xml version="1.0" encoding="utf-8"?>
<AuthTokenRequest 
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
  xmlns="http://ksef.mf.gov.pl/auth/token/2.0">
   <Challenge>20250604-CR-461EA5B000-537A6BA15D-D7</Challenge>
   <ContextIdentifier>
   	<Nip>1234563218</Nip>
   </ContextIdentifier>
   <SubjectIdentifierType>certificateSubject</SubjectIdentifierType>
   <ds:Signature xmlns:ds="http://www.w3.org/2000/09/xmldsig#" Id="Signature-9707709">
        <!-- Tu powinien być podpis XAdES -->
   </ds:Signature>
</AuthTokenRequest>
```

REZULTAT:

* _referenceNumber_  <= zawsze \
  Numer referencyjny operacji uwierzytelnienia (będzie potrzebny w dalszych krokach)

* _authenticationToken_  <= zawsze \
  tymczasowy token dostępowy do operacji (struktura dwóch pól):

  * _token_ <= zawsze \
    token (JWT).

  * _validUntil_ <= zawsze \
    "termin ważności" tokena (UTC!).

Przykład:
```json
{
  "referenceNumber": "20250514-AU-2DFC46C000-3AC6D5877F-D4",
  "authenticationToken": 
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
    "validUntil": "2025-07-11T12:23:56.015+00:00"
  }
}
```
### GetAuthStatus
Zwraca bieżący status operacji uwierzytelnienia dla podanego tokena

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _referenceNumber_ <= zawsze \
Numer referencyjny, otrzymany od [SubmitXadesAuthRequest](#SubmitXadesAuthRequest).

* _authToken_ <= zawsze \
token, dostarczony przez [SubmitXadesAuthRequest](#SubmitXadesAuthRequest) w sekcji _AuthorizationToken_.

Przykład:

```json
{
  "referenceNumber": "20250514-AU-2DFC46C000-3AC6D5877F-D4",
  "authToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT:
* _startDate_ <= zawsze \
Data rozpoczęcia procesu uwierzytelnienia

* _authenticationMethod_ <= zawsze \
Metoda uwierzytelnienia Możliwe wartości: "TrustedProfile", "InternalCertificate", "QualifiedSignature", "QualifiedSeal", "PersonalSignature". (W przypadku KSeF.Services nie implementujemy logowania tokenem, stąd nie wystąpi nigdy wartość "Token").

* _status_ <= zawsze \
Struktura z aktualnym stanem procesu:
   * _code_ <= zawsze \
Najważniejsze pole, gdy czekamy na zakończenie procesu uwierzytelnienia. 100, gdy proces jeszcze trwa, 200, gdy już można pobrać tokeny dostępowe. Wyższe kody oznaczają jakiś błąd (por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Uzyskiwanie-dostepu/paths/~1api~1v2~1auth~1%7BreferenceNumber%7D/get))
  * _description_ <= zawsze \
Opis statusu (przydaje się w przypadku błędu)

  * _details_ \
opcjonalna lista akapitów z dodatkowym opisem

* _isTokenRedeemed_
Wartość **true**, jeżeli token został już sesji pobrany (można to zrobić tylko raz)

* _refreshTokenValidUntil_
Termin ważności refresh token

Przykład:

```json
{
    "startDate": "2025-10-23T12:43:15+00:00",
    "authenticationMethod": "QualifiedSeal",
    "status": 
    {
        "code": 100,
        "description": "Uwierzytelnianie w toku"
    }

}
```
### GetAccessTokens
Pobiera tokeny dostępowe sesji uwierzytelnienia. Ta operacja wymaga, by status sesji (por. [GetAuthStatus](#getauthstatus)) był **200**. Można ją wykonać tylko raz.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _authToken_ <= zawsze \
token, dostarczony przez [SubmitXadesAuthRequest](#submitxadesauthrequest) w sekcji _AuthorizationToken_.

```json
{
  "authToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT:
> Dwie identyczne struktury: jedna z aktualnym tokenem dostępowym, druga, ważniejsza, z tokenem do tworzenia nowych tokenów dostępowych:
* _accessToken_  <= zawsze \
  tymczasowy token dostępowy do operacji (struktura dwóch pól):

  * _token_ <= zawsze \
    token (JWT).

  * _validUntil_ <= zawsze \
    "termin ważności" tokena (UTC!).

* _refreshToken_  <= zawsze \
  Token, którego należy użyć do tworzenia nowych tokenów  (struktura dwóch pól):

  * _token_ <= zawsze \
    token (JWT).

  * _validUntil_ <= zawsze \
    "termin ważności" tokena (UTC!).
Przykład:

```json
{
    "accessToken": 
    {
         "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiQ29udGV4dFRva2VuIiwiY29udGV4dC1pZGVudGlmaWVyLXR5cGUiOiJOaXAiLCJjb250ZXh0LWlkZW50aWZpZXItdmFsdWUiOiIzNzU2OTc3MDQ5IiwiYXV0aGVudGljYXRpb24tbWV0aG9kIjoiUXVhbGlmaWVkU2VhbCIsInN1YmplY3QtZGV0YWlscyI6IntcIlN1YmplY3RJZGVudGlmaWVyXCI6e1wiVHlwZVwiOlwiTmlwXCIsXCJWYWx1ZVwiOlwiMzc1Njk3NzA0OVwifX0iLCJleHAiOjE3NDcyMjAxNDksImlhdCI6MTc0NzIxOTI0OSwiaXNzIjoia3NlZi1hcGktdGkiLCJhdWQiOiJrc2VmLWFwaS10aSJ9.R_3_R2PbdCk8T4WP_0XGOO1iVNu2ugNxmkDvsD0soIE",
         "validUntil": "2025-07-11T12:23:56.0154302+00:00"
    },

    "refreshToken": 
    {
        "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiQ29udGV4dFRva2VuIiwiY29udGV4dC1pZGVudGlmaWVyLXR5cGUiOiJOaXAiLCJjb250ZXh0LWlkZW50aWZpZXItdmFsdWUiOiIzNzU2OTc3MDQ5IiwiYXV0aGVudGljYXRpb24tbWV0aG9kIjoiUXVhbGlmaWVkU2VhbCIsInN1YmplY3QtZGV0YWlscyI6IntcIlN1YmplY3RJZGVudGlmaWVyXCI6e1wiVHlwZVwiOlwiTmlwXCIsXCJWYWx1ZVwiOlwiMzc1Njk3NzA0OVwifX0iLCJleHAiOjE3NDcyMjAxNDksImlhdCI6MTc0NzIxOTI0OSwiaXNzIjoia3NlZi1hcGktdGkiLCJhdWQiOiJrc2VmLWFwaS10aSJ9.R_3_R2PbdCk8T4WP_0XGOO1iVNu2ugNxmkDvsD0soIE",
        "validUntil": "2025-07-11T12:23:56.0154302+00:00"
    }
}
```
### RefreshAccessToken
Tworzy nowy token dostępowy za pomocą "tokenu odświeżenia" (refresh token).

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _refreshToken_ <= zawsze \
token, dostarczony przez [GetAccessToken](#getaccesstoken) w sekcji _refreshToken_.

```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT:
> Struktura z nowym tokenem dostępowym
* _accessToken_  <= zawsze \
  token dostępowy do operacji (struktura dwóch pól):

  * _token_ <= zawsze \
    token (JWT).

  * _validUntil_ <= zawsze \
    "termin ważności" tokena (UTC!).

Przykład:

```json
{
    "accessToken": 
    {
         "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiQ29udGV4dFRva2VuIiwiY29udGV4dC1pZGVudGlmaWVyLXR5cGUiOiJOaXAiLCJjb250ZXh0LWlkZW50aWZpZXItdmFsdWUiOiIzNzU2OTc3MDQ5IiwiYXV0aGVudGljYXRpb24tbWV0aG9kIjoiUXVhbGlmaWVkU2VhbCIsInN1YmplY3QtZGV0YWlscyI6IntcIlN1YmplY3RJZGVudGlmaWVyXCI6e1wiVHlwZVwiOlwiTmlwXCIsXCJWYWx1ZVwiOlwiMzc1Njk3NzA0OVwifX0iLCJleHAiOjE3NDcyMjAxNDksImlhdCI6MTc0NzIxOTI0OSwiaXNzIjoia3NlZi1hcGktdGkiLCJhdWQiOiJrc2VmLWFwaS10aSJ9.R_3_R2PbdCk8T4WP_0XGOO1iVNu2ugNxmkDvsD0soIE",
         "validUntil": "2025-07-11T12:23:56.0154302+00:00"
    }
}
```
### OpenOnlineSession
Rozpoczyna interaktywną sesję wysyłania faktur sprzedaży.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _invoiceFormat_ <= zawsze \
Struktura opisująca format faktur, które będą wysyłane

  * _systemCode_ <= zawsze \
Np. "FA (3)".

  * _schemaVersion_ <= zawsze \
Np. "1-0E"

  * _value_ <= zawsze \
Np. "FA"

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

```json
{
  "formCode": 
   {
    "systemCode": "FA (3)",
    "schemaVersion": "1-0E",
    "value": "FA"
   },
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU"
}
```

REZULTAT: \
Dane, które należy przekazać w parametrach wejściowych do żądania [SendOnlineSessionInvoice](#sendonlinesessioninvoice):

* _referenceNumber_  <= zawsze \
  Numer referencyjny sesji

* _encryption_  <= zawsze \
  Informacje o szyfrowaniu, które należy zastosować dla faktur wysyłanych w tej sesji

  * _base64Key_ <= zawsze \
    Klucz szyfrowania (enkodowany w Base64).

  * _base64Mix_ <= zawsze \
    Tzw. "sól" kryptograficzna, lub "wektor inicjalizacji" (IV), towarzyszące kluczowi (także enkodowana w Base64)

Przykład:

```json
{
  "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
  "encryption": 
  {
    "base64Key": "bdUVjqLj+y2q6aBUuLxxXYAMqeDuIBRTyr+hB96DaWKaGzuVHw9p+Nk9vhzgF/Q5cavK2k6eCh6SdsrWI0s9mFFj4A4UJtsyD8Dn3esLfUZ5A1juuG3q3SBi/XOC/+9W+0T/KdwdE393mbiUNyx1K/0bw31vKJL0COeJIDP7usAMDl42/H1TNvkjk+8iZ80V0qW7D+RZdz+tdiY1xV0f2mfgwJ46V0CpZ+sB9UAssRj+eVffavJ0TOg2b5JaBxE8MCAvrF6rO5K4KBjUmoy7PP7g1qIbm8xI2GO0KnfPOO5OWj8rsotRwBgu7x19Ine3qYUvuvCZlXRGGZ5NHIzWPM",
    "base64Mix": "OmtDQdl6vkOI1GLKZSjgEg=="
  }
}
```
### SendOnlineSessionInvoice

Wysyła do KSeF (w sesji interaktywnej) wskazany plik z fakturą sprzedaży.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _invoiceFile_ <= zawsze \
Ścieżka (może być względna) do pliku XML z fakturą, która ma być wysłana

* _offlineMode_ \
Opcjonalny. Domyślnie - **false**. Wpisz z wartością **true** (bez cudzysłowu) gdy wizualizacja przesyłanej <u>dzisiejszej</u> faktury z kodem QR II została wcześniej wysłana do Klienta.

* _hashOfCorrectedInvoice_ \
Opcjonalny. Stosowany wyłącznie w przypadku tzw. "korekt technicznych", czyli sytuacji, gdy faktura wysłana z _"offlineMode":true_ została odrzucona przez KSeF. Wtedy należy przygotować jej poprawioną wersję (nowy plik XML) i wysłać powtórnie, wpisując w pole _hashOfCorrectedInvoice_ skrót tej odrzuconej faktury.

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _encryption_  <= zawsze \
Informacje o szyfrowaniu, otrzymane w informacji zwrotnej od [OpenOnlineSession](#openonlinesession)

  * _base64Key_ <= zawsze \
Klucz szyfrowania (enkodowany w Base64).

  * _base64Mix_ <= zawsze \
wektor inicjalizacji (IV), towarzysząca kluczowi

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "invoiceFile":"../../Inbox/FV293044_2026.xml"
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "encryption": 
    {
      "base64Key": "bdUVjqLj+y2q6aBUuLxxXYAMqeDuIBRTyr+hB96DaWKaGzuVHw9p+Nk9vhzgF/Q5cavK2k6eCh6SdsrWI0s9mFFj4A4UJtsyD8Dn3esLfUZ5A1juuG3q3SBi/XOC/+9W+0T/KdwdE393mbiUNyx1K/0bw31vKJL0COeJIDP7usAMDl42/H1TNvkjk+8iZ80V0qW7D+RZdz+tdiY1xV0f2mfgwJ46V0CpZ+sB9UAssRj+eVffavJ0TOg2b5JaBxE8MCAvrF6rO5K4KBjUmoy7PP7g1qIbm8xI2GO0KnfPOO5OWj8rsotRwBgu7x19Ine3qYUvuvCZlXRGGZ5NHIzWPM",
      "base64Mix": "OmtDQdl6vkOI1GLKZSjgEg=="
    } 
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT:
Dane, które należy przekazać w parametrach wejściowych SendOnlineSessionInvoice:

* _invoiceReferenceNumber_  <= zawsze \
Numer referencyjny faktury (do użycia w dalszych żądaniach związanych z jej obsługą)

* _invoiceHash_  <= zawsze \
Skrót wysłanego pliku faktury. Pojawia się potem w wielu miejscach, więc można go np. wpisać do bazy danych obok numeru referencyjnego.

Przykład:
```json
{
    "invoceReferenceNumber": "20250625-EE-319D7EE000-B67F415CDC-2C",
    "invoiceHash":"TJF1YWseJ/SIs4\u002BeY25bsm\u002BtTxp0A/H7PYbCgu17024="
}
```
>[!NOTE]
>Faktury z datą sprzedaży (pole _P_1_) < daty dzisiejszej są przez KSeF oznaczane jako **Offline**, niezależnie od ustawionego znacznika *offlineMode*

### CloseOnlineSession

_[=> spis treści](#spis-tresci)_

Kończy interaktywną sesję wysyłania faktur sprzedaży.

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT: brak

### OpenBatchSession
Tworzy wsadową sesję wysyłania faktur sprzedaży, wykonując upload na serwer odpowiednio zaszyfrowanych plików ze wskazanymi fakturami.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:
* _invoiceFormat_ <= zawsze \
Struktura opisująca format faktur, które będą wysyłane

  * _systemCode_ <= zawsze \
Np. "FA (3)".

  * _schemaVersion_ <= zawsze \
Np. "1-0E"

  * _value_ <= zawsze \
Np. "FA"

* _srcFolder_ <= zawsze \
Folder, w którym znajdują się pliki faktur (może być względny)

* _files_ <= zawsze \
Lista plików do wysłania (same nazwy, bez ścieżek)

* _offlineMode_ \
Opcjonalny. Domyślnie - **false**. Wpisz z wartością **true** (bez cudzysłowu) gdy wizualizacje przesyłanych <u>dzisiejszych</u> faktur z kodem QR II zostały wcześniej wysłane do Klienta.

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

```json
{
  "formCode": 
   {
    "systemCode": "FA (3)",
    "schemaVersion": "1-0E",
    "value": "FA"
   },
   "srcFolder":"..\\..\\..\\Inbox",
   "files": 
   [
		"012345-2025.xml",
		"012346-2025.xml",
		"012347-2025.xml",
		"012348-2025.xml",
		"012349-2025.xml"
   ],
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU"
}
```

REZULTAT: \

* _referenceNumber_  <= zawsze \
  Numer referencyjny uwtorzonej sesji wsadowej

Przykład:

```json
{
  "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
}
```

### CloseBatchSession
Kończy tworzenie wsadowej sesji wysyłania faktur sprzedaży.
>[!NOTE]
>To polecenie uruchamia przetwarzanie "wsadu" fakur, stworzonego i wysłanego na serwer KSeF za pomocą metody [OpenBatchSession](#openbatchsession).

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

REZULTAT: brak

### GetSessionStatus

Podaje status wskazanej sesji wysyłania faktur (tak interaktywnej, jak i wsadowej)

Metadane sesji zawierją url do jej UPO. Trochę oportunistycznie, dodałem temu żądaniu "efekt uboczny" w postaci zapisania tego UPO do pliku o podanej nazwie. W ten sposób nie trzeba wywoływać oddzielnego żądania, które to realizuje.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _saveUpoAs_ \
Opcjonalny: wpisz tu ścieżkę do pliku, w którym chcesz mieć zapisane zbiorcze UPO faktur przesłanych w tej sesji. (Stworzy go, jeżeli sesja ma status 200)

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```
>[!NOTE]
>1. UPO nie będą zapisane, jeżeli plik o nazwie podanej w argumencie _saveUpoAs_ już istnieje. 
>2. UPO może zawierać max. 10 000 faktur. Jeżeli sesja ma więcej niż jedno UPO (przy obecnych limitach to wysoce wątpliwe) to **drugi** plik o nazwie _saveUpoAs_ otrzyma przyrostek "-1, trzeci - "-2", itd.


ZWRACA:

* _status_ <= zawsze \
Struktura z aktualnym stanem procesu:
   * _code_ <= zawsze \
Najważniejsze pole, gdy czekamy na zakończenie sesji. 100, gdy jest otwarta, 170 gdy zamknięta, i 200, gdy przetwarzanie wysłanych faktur zostało zakończone. Wyższe kody oznaczają jakiś błąd (por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Status-wysylki-i-UPO/paths/~1api~1v2~1sessions~1%7BreferenceNumber%7D/get))
  * _description_ <= zawsze \
Opis statusu (przydaje się w przypadku błędu)

  * _details_ \
opcjonalna lista akapitów z dodatkowym opisem

* _validUntil_ \
Termin ważności sesji (gdy status = 100).

* _upo_ \
Tylko dla sesji o statusie 200. Linki do pobrania zbiorczego UPO sesji. (Szczegóły - por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Status-wysylki-i-UPO/paths/~1api~1v2~1sessions~1%7BreferenceNumber%7D/get)).

* _invoiceCount_ \
Liczba otrzymanych od Nadawcy faktur

* _successfulInvoiceCount_ \
Liczba faktur przetworzonych poprawnie

* _failedlInvoiceCount_ \
Liczba faktur odrzuconych (trzeba korzystając z indywidualnych statusów faktury sprawdzić, która i dlaczego).

```json
{
    "status": 
   {
         "code": 200,
         "description": "Sesja interaktywna przetworzona pomyślnie"
    },
    "upo": 
    {

    	"pages": 
	[
            {
                "referenceNumber": "20250626-EU-2EBD6FA000-242EB9B66D-43",
                "downloadUrl": "https://ksef-api/api/v2/sessions/20250626-SO-2EBAD16000-2429DECA8E-E2/upo/20250626-EU-2EBD6FA000-242EB9B66D-43"
            }
        ]
    },

    "invoiceCount": 10,
    "successfulInvoiceCount": 8,
    "failedInvoiceCount": 2
}
```

### GetSessionInvoice

Zwraca status przetworzenia wskazanej faktury wysłanej do KSeF.

Metdane faktury zawierają url do jej UPO. Trochę oportunistycznie, dodałem temu żądaniu "efekt uboczny" w postaci zapisania UPO do pliku o wskazanej nazwie.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _invoiceReferenceNumber_  <= zawsze \
Numer referencyjny faktury (zwrócony przez [SendOnlineSessionInvoice](#sendonlinesessioninvoice)).

* _saveUpoAs_ \
Opcjonalny: wpisz tu ścieżkę do pliku, w którym chcesz mieć zapisane UPO tej faktury. (Stworzy je, jeżeli faktura ma status 200)

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "invoiceRreferenceNumber": "20250625-EE-319D7EE000-B67F415CDC-2C"
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

* _ordinalNumber_ <= zawsze \
Numer sekwencyjny faktury w sesji.

* _invoiceNumber_ \
Numer faktury (nadany przez Nadawcę - wartość pola P_2 z FA(3))

* _ksefNumber_ \
Numer KSeF, jaki otrzymała faktura (gdy jej status = 200)

* _refrenceNumber_ \
Odpowiada _invoiceReferenceNumber_ z argumentów tego żądania

* _invoiceHash_ \
Skrót pliku faktury

* _invoiceFileName_ \
Nazwa pliku faktury - zwracana tylko dla faktur wysyłanych w sesji wsadowej (batch).

* _invoicingDate_ <= zawsze \
Czas otrzymania faktury przez KSeF (tzn. gdy uzyskała status 100)

* _acquisitionDate_ \
Czas nadania numeru KSeF (tzn. gdy uzyskała status 200)

* _status_ <= zawsze \
Struktura z aktualnym stanem przetwarzania faktury:
   * _code_ <= zawsze \
Najważniejsze pole, gdy czekamy wynik: 100, gdy otrzymana, 150 gdy przetwarzana, i 200, gdy została przyjęta. Wyższe kody oznaczają jakiś błąd (por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Status-wysylki-i-UPO/paths/~1api~1v2~1sessions~1%7BreferenceNumber%7D~1invoices~1%7BinvoiceReferenceNumber%7D/get))
  * _description_ <= zawsze \
Opis statusu (przydaje się w przypadku błędu)
  * _details_ \
opcjonalna lista akapitów z dodatkowym opisem

Przykład:
```json
{
    "ordinalNumber": 2,
    "referenceNumber": "20250626-EE-2F20AD2000-242386DF86-52",
    "invoicingDate": "2025-07-11T12:23:56.0154302+00:00",
    "status": 
	{
           "code": 440,
           "description": "Duplikat faktury",
           "details": 
            [
                "Duplikat faktury. Faktura o numerze KSeF: 5265877635-20250626-010080DD2B5E-26 została już prawidłowo przesłana do systemu w sesji: 20250626-SO-2F14610000-242991F8C9-B4"
            ]
        }
}
```

### GetSessionInvoiceUpo

Zapisuje na dysku dokument UPO faktury przyjętej do KSeF.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _invoiceReferenceNumber_  <= zawsze \
Numer referencyjny faktury (zwrócony przez [SendOnlineSessionInvoice](#sendonlinesessioninvoice)).

* _dstFile_ <= zawsze \
Ścieżka dla wynikowego (podpisanego) pliku _*.xml_. (Może być względna).

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "invoiceRreferenceNumber": "20250625-EE-319D7EE000-B67F415CDC-2C",
   "dstFile": "../../UPO/FV1233_2026_UPO.xml",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:
* _upoFile_ <= zawsze \
Pełna ścieżka dla wynikowego (podpisanego) pliku _*.xml_. (Ewentuanlne rozwinięcie *dstFile*. Tak - dla potwierdzenia).

Przykład:
```json
{
	upoFile: "C:\\Users\\Hyperbook\\KSeF\\UPO\\FV1233_2026_UPO.xml"
}
```
### ListSessionInvoices

Pobiera listę (metadane) faktur przetworzonych przez sesję. W przypadku dużej liczby faktur przekraczajacej *pageSize* zwraca *continuationToken*, umożliwiający pobranie reszty w kolejnym wywołaniu.

Metadane pobranych faktur z wysyłek wsadowych zawierają oryginalną nazwę pliku i url-e do indywidualnych UPO. Trochę oportunistycznie, dodałem temu żądaniu "efekt uboczny" w postaci pobierania ich UPO do plików we wskazanym folderze.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny sesji

* _pageSize_ \
Max. liczba faktur na zwracanej liście. Domyślnie: 100.

* _continuationToken_ \
Opcjonalny: token zwrócony w wyniku poprzedniego wywołania, gdy liczba faktur do pobrania jest większa od *pageSize*.

* _saveUpoTo_ \
Opcjonalny: wpisz tu ścieżkę do folderu, w którym chcesz mieć zapisane indywidualne UPO faktur przesłanych w tej sesji. 
>[!NOTE]
>UPO faktur zostaną zapisane do folderu _saveUpoTo_ **tylko dla sesji wsadowych** o statusie 200.

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "pageSize":50,
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

Listę metadanych faktur sesji oraz ewentualnie *continuationToken*, jeżeli są jeszcze dalsze dane do pobrania. Każda z faktur jest opisana za pomocą takiej samej struktury, jak struktura zwracana przez [GetSessionInvoice](#getsessioninvoice).

Przykład:
```json
{
    "continuationToken": "W34idG9rZW4iOiIrUklEOn4xUE5BQU5hcXJVOUFBQUFBQUFBQUFBPT0jUlQ6MSNUUkM6MTAjSVNWOjIjSUVPOjY1NTY3I1FDRjo4I0ZQQzpBVUFBQUFBQUFBQUFRZ0FBQUFBQUFBQT0iLCJyYW5nZSI6eyJtaW4iOiIiLCJtYXgiOiJGRiJ9fV0=",
    "invoices": 
	[
		{
			"ordinalNumber": 1,
			"invoiceNumber": "FA/XPWIC-7900685789/06/2025",
			"ksefNumber": "5265877635-20250626-010080DD2B5E-26",
			"referenceNumber": "20250626-EE-2F15D39000-242207E5C4-1B",
			"invoiceHash": "mkht+3m5trnfxlTYhq3QFn74LkEO69MFNlsMAkCDSPA=",
			"acquisitionDate": "2025-07-11T12:24:16.0154302+00:00",
			"invoicingDate": "2025-07-11T12:23:56.0154302+00:00",
			"permanentStorageDate": "2025-07-11T12:24:01.0154302+00:00",
			"upoDownloadUrl": "https://ksef-test.mf.gov.pl/storage/01/20250918-SB-3789A40000-20373E1269-A3/invoice-upo/upo_5265877635-20250626-010080DD2B5E-26.xml?sv=2025-01-05&st=2025-09-18T14%3A49%3A20Z&se=2025-09-21T14%3A54%3A20Z&sr=b&sp=r&sig=%2BUWFPA10gS580VhngGKW%2FZiOOtiHPOiTyMlxhG6ZvWs%3D",
			"status": 
			{
				"code": 200,
				"description": "Sukces"
			}
		},
		{
			"ordinalNumber": 2,
			"referenceNumber": "20250626-EE-2F20AD2000-242386DF86-52",
			"invoiceHash": "mkht+3m5trnfxlTYhq3QFn74LkEO69MFNlsMAkCDSPA=",
			"invoicingDate": "2025-07-11T12:23:56.0154302+00:00",
			"status": 
			{
				"code": 440,
				"description": "Duplikat faktury",
				"details": 
						[
							"Duplikat faktury. Faktura o numerze KSeF: 5265877635-20250626-010080DD2B5E-26 została już prawidłowo przesłana do systemu w sesji: 20250626-SO-2F14610000-242991F8C9-B4"
						]
		   }
		}
	]
}
```
### ListSubjectInvoices

Pobiera listę (metadane) faktur związanych w jakikolwiek sposób z aktualnym kontekstem (tj. NIP): musi w nich występować albo w roli Sprzedawcy ("Subject1"), Nabywcy ("Subject2"), strony trzeciej ("Subject3") lub podmiotu upoważnionego ("SubjectAuthorized"). Rezultaty są uporządkowane rosnąco wg daty. 

W przypadku, gdy liczba faktur przekracza *pageSize*, znacznik *hasMore* w zwróconym rezultacie ma wartość *true*. Należy wówczas tę metodę wywołać jeszcze raz, zwiększając odpowiednio parametr *pageOffset*.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _filters_  <= zawsze \
warunek WHERE wyszukiwania faktur. Szczegółowy opis - por. dokumentacja API, [Pobranie listy metadanych faktur](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1query~1metadata/post).

* _pageOffset_ \
Indeks strony wyników. Domyślnie: 0.

* _pageSize_ \
Max. liczba faktur na zwracanej liście. Domyślnie: 250.

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "filters": 
   {
		"subjectType":"Subject2",
		"dateRange":
		{
			"dateType":"PermanentStorage",
			"from": "2025-02-01 00:00:00Z"
		},
		"formType":"FA"
   },
   "pageSize":50,
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

Strukturę zawierającą listę metadanych faktur:

* _hasMore_ <= zawsze \
Znacznik, czy istnieją dalsze elementy do pobrania (*true*/*false*).

* _invoices_  <= zawsze \
Lista metadanych faktur. Szczegółowy opis - por. dokumentacja API, [Pobranie listy metadanych faktur](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1query~1metadata/post).

Przykład:
```json
{
    "hasMore": false,
    "invoices": 
		[
			{
				"ksefNumber": "5555555555-20250828-010080615740-E4",
				"invoiceNumber": "FA/KUDYO1a7dddfe-610e-4843-84ba-6b887e35266e",
				"issueDate": "2025-08-27",
				"invoicingDate": "2025-08-28T09:22:13.388+00:00",
				"acquisitionDate": "2025-08-28T09:22:56.388+00:00",
				"permanentStorageDate": "2025-08-28T09:23:01.388+00:00",
				"seller": 
				{
					"nip": "5555555555",
					"name": "Test Company 1"
				},
				"buyer": 
				{
					"identifierType": "Nip",
					"identifier": "7352765225",
					"name": "Test Company 4"
				},
				"netAmount": 35260.63,
				"grossAmount": 43370.57,
				"vatAmount": 8109.94,
				"currency": "PLN",
				"invoicingMode": "Offline",
				"invoiceType": "Vat",
				"formCode": 
				{
					"systemCode": "FA (3)",
					"schemaVersion": "1-0E",
					"value": "FA"
				},
				"isSelfInvoicing": false,
				"hasAttachment": false,
				"invoiceHash": "mkht+3m5trnfxlTYhq3QFn74LkEO69MFNlsMAkCDSPA=",
				"thirdSubjects": [ ]
			},
			{
				"ksefNumber": "5555555555-20250828-010080615740-E4",
				"invoiceNumber": "5265877635-20250925-010020A0A242-0A",
				"issueDate": "2025-08-28",
				"invoicingDate": "2025-08-28T10:23:13.388+00:00",
				"acquisitionDate": "2025-08-28T10:23:56.388+00:00",
				"permanentStorageDate": "2025-08-28T10:24:01.388+00:00",
				"seller": 
				{
					"nip": "5555555555",
					"name": "Test Company 1"
				},
				"buyer": 
				{
					"identifierType": "Nip",
					"identifier": "3225081610",
					"name": "Test Company 2"
				},
				"netAmount": 35260.63,
				"grossAmount": 43370.57,
				"vatAmount": 8109.94,
				"currency": "PLN",
				"invoicingMode": "Online",
				"invoiceType": "Vat",
				"formCode": 
				{
					"systemCode": "FA (3)",
					"schemaVersion": "1-0E",
					"value": "FA"
				},
				"isSelfInvoicing": false,
				"hasAttachment": true,
				"invoiceHash": "o+nMBU8n8TAhy6EjbcdYdHSZVbUspqmCKqOPLhy3zIQ=",
				"thirdSubjects": 
    			[
					{
						"identifierType": "InternalId",
						"identifier": "5555555555-12345",
						"name": "Wystawca faktury",
						"role": 4
					}
				]
			}
		]
}
```
### GetInvoice

Zapisuje na dysku pojedynczej faktury o podanym numerze KSeF.
>[!NOTE]
>Żądana faktura musi być w jakikolwiek sposób związana z aktualnym kontekstem (tj. NIP): musi w nich występować albo w roli Sprzedawcy (*Podmiot1*), Nabywcy (*Podmiot2*), strony trzeciej (*Podmiot3*) lub podmiotu upoważnionego (*PodmiotUpowazniony*).

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _ksefNumber_  <= zawsze \
Numer KSeF faktury.

* _dstFile_ <= zawsze \
Ścieżka dla zapisania wynikowego pliku _*.xml_. (Może być względna).

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "ksefNumber": "5555555555-20250828-010080615740-E4",
   "dstFile": "../../Received/5555555555-20250828-010080615740-E4.xml",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:
* _invoiceFile_ <= zawsze \
Pełna ścieżka dla wynikowego pliku _*.xml_. (Ewentuanlne rozwinięcie *dstFile*. Tak - dla potwierdzenia).

Przykład:
```json
{
	invoiceFile: "C:\\Users\\Hyperbook\\KSeF\\Received\\5555555555-20250828-010080615740-E4.xml"
}
```
### SubmitInvoicesRequest

Inicjuje żądanie pobrania paczki faktur, związanych w jakikolwiek sposób z aktualnym kontekstem (tj. NIP): musi w nich występować albo w roli Sprzedawcy ("Subject1"), Nabywcy ("Subject2"), strony trzeciej ("Subject3") lub podmiotu upoważnionego ("SubjectAuthorized"). Faktury w paczce są uporządkowane rosnąco wg daty. 

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _filters_  <= zawsze \
warunek WHERE wyszukiwania faktur. To taka sama struktura, jak w żądaniu [ListSubjectInvoices](#listsubjectinvoices). Szczegółowy opis - por. dokumentacja API, [Pobranie listy metadanych faktur](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1query~1metadata/post).

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "filters": 
   {
		"subjectType":"Subject2",
		"dateRange":
		{
			"dateType":"PermanentStorage",
			"from": "2025-02-01 00:00:00Z"
		},
		"formType":"FA"
   },
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

Strukturę z numerem referencyjnym żądania i danymi towarzyszącymi:

* _referenceNumber_ <= zawsze \
Numer referencyjny żądania (do sprawdzania stanu i pobrania rezultatów).

* _encryption_  <= zawsze \
  Informacje o szyfrowaniu, które należy zastosować dla odszyfrowania otrzymanych paczek z fakturami. Tę strukturę należy przekazać do żądania [DownloadInvoices](#downloadinvoices)

  * _base64Key_ <= zawsze \
    Klucz szyfrowania (enkodowany w Base64).

  * _base64Mix_ <= zawsze \
    Tzw. "sól" kryptograficzna, lub "wektor inicjalizacji" (IV), towarzyszące kluczowi (także enkodowana w Base64)

Przykład:

```json
{
  "referenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
  "encryption": 
	{
		"base64Key": "yvhR5JQa0hEEPmsi/d1aLSfiu0N3c36ezDEapyk6JZU=",
		"base64Mix": "TZxl3IUHbxc9IpLn00WoOA=="
	}
}
```

### GetInvoicesRequestStatus

Podaje status żądania pobrania (w API nazwya się to "eksportem") faktur

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _referenceNumber_  <= zawsze \
Numer referencyjny żądania

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "operationReferenceNumber": "20250625-SO-2C3E6C8000-B675CF5D68-07",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```

ZWRACA:

* _status_ <= zawsze \
Struktura z aktualnym stanem procesu:
   * _code_ <= zawsze \
Najważniejsze pole, gdy czekamy na rezultat. 100, gdy jest przygotowywany, i 200, gdy jest gotowy do pobrania. Wyższe kody oznaczają jakiś błąd (por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1exports~1%7BoperationReferenceNumber%7D/get))
  * _description_ <= zawsze \
Opis statusu (przydaje się w przypadku błędu)

  * _details_ \
opcjonalna lista akapitów z dodatkowym opisem

* _completeDate_ \
Czas udostępnienia (gdy status = 200).

* _package_ \
Tylko dla sesji o statusie 200. Linki do pobrania paczek z fakturami. (Szczegóły - por. [dokumentacja API](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1exports~1%7BoperationReferenceNumber%7D/get)). Tę strukturę należy przekazać do żądania [DownloadInvoices](#downloadinvoices).


```json
{
    "status": 
	{
		"code": 200,
		"description": "Eksport faktur zakończony sukcesem"
	},
	"completedDate": "2025-09-16T16:09:40.901091+00:00",
	"package": 
	{
		"invoiceCount": 10000,
		"size": 22425060,
		"parts": 
			[
				{
					"ordinalNumber": 1,
					"partName": "20250925-EH-2D2C11B000-E9C9ED8340-EE-001.zip.aes",
					"method": "GET",
					"url": "https://ksef-api-storage/storage/00/20250626-eh-2d2c11b000-e9c9ed8340-ee/invoice-part/20250925-EH-2D2C11B000-E9C9ED8340-EE-001.zip.aes?skoid=1ad7cfe8-2cb2-406b-b96c-6eefb55794db&sktid=647754c7-3974-4442-a425-c61341b61c69&skt=2025-06-26T09%3A40%3A54Z&ske=2025-06-26T10%3A10%3A54Z&sks=b&skv=2025-01-05&sv=2025-01-05&se=2025-06-26T10%3A10%3A54Z&sr=b&sp=w&sig=8mKZEU8Reuz%2Fn7wHi4T%2FY8BzLeD5l8bR2xJsBxIgDEY%3D",
					"partSize": 22425060,
					"partHash": "BKH9Uy1CjBFXiQdDUM2CJYk5LxWTm4fE1lljnl83Ajw=",
					"encryptedPartSize": 22425072,
					"encryptedPartHash": "HlvwRLc59EJH7O5GoeHEZxTQO5TJ/WP1QH0aFi4x2Ss=",
					"expirationDate": "2025-09-16T17:09:40.901091+00:00"
				}
			],
			"isTruncated": true,
			"lastPermanentStorageDate": "2025-09-11T11:40:40.266578+00:00"
	}
}
```

### DownloadInvoices
Pobiera paczki z fakturami i rozpakowauje ich zawartość (pliki *.XML faktur) do wskazanego folderu na dysku. Zwraca listę metadanych pobranych faktur.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _dstFolder_  <= zawsze \
Folder, w którym mają być zapisane pliki XML faktur z paczki

* _encryption_  <= zawsze \
  Informacje o szyfrowaniu, które należy zastosować dla odszyfrowania otrzymanych paczek z fakturami. To struktura uzyskana z żądania [SubmitInvoicesRequest](#submitinvoicesrequest)

* _package_  <= zawsze \
  pakiet informacji o paczce faktur udostępnionej do pobrania. To struktura uzyskana z żądania [GetInvoicesRequestStatus](#getinvoicesrequeststatus)


Przykład:
```json
{
	"dstFolder":"C:\\Users\\Hyperbook\\Documents\\ZigZak\\KSeF\\Connector\\data\\TEST\\9999999999\\Results\\Received\\",
	"encryption": 
	{
		"base64Key": "yvhR5JQa0hEEPmsi/d1aLSfiu0N3c36ezDEapyk6JZU=",
		"base64Mix": "TZxl3IUHbxc9IpLn00WoOA=="
	},
    "package": 
	{
        "invoiceCount": 10,
        "size": 16784,
        "parts": 
		[
            {
                "ordinalNumber": 1,
                "partName": "20251124-EH-344038D000-599E353693-13-001.zip.aes",
                "method": "GET",
                "url": "https://ksef-test.mf.gov.pl/storage/05/20251124-eh-344038d000-599e353693-13/invoice-package/20251124-EH-344038D000-599E353693-13-001.zip.aes?skoid=0e92608a-831d-404b-9945-197ed82a5dbc&sktid=647754c7-3974-4442-a425-c61341b61c69&skt=2025-11-22T02%3A41%3A34Z&ske=2025-11-29T02%3A41%3A34Z&sks=b&skv=2025-01-05&sv=2025-01-05&st=2025-11-24T15%3A08%3A25Z&se=2025-11-24T16%3A13%3A25Z&sr=b&sp=r&sig=NPcyvcTshDTZF4ZWEMbXGLyNgqEICM1vLAXPlhW2ewY%3D",
                "partSize": 16784,
                "partHash": "7FG7M+KeM1DTc16GlG05phExzEWYX4jwlItU3Cu9z38=",
                "encryptedPartSize": 16800,
                "encryptedPartHash": "VearButtwE+SgKyZ0UELllRHQlmPYbtnxivhIgq/vUw=",
                "expirationDate": "2025-11-24T16:13:25.945309+00:00"
            }
        ],
        "isTruncated": false
    }
}
```

ZWRACA:

* _isTruncated_ <= zawsze \
Wartość _true/false_, przepisana z _package_ (wstawiłem ją tak "dla porządku"). _True_, gdy pobrano 10 tys. faktur i nie są to wszystkie dokumenty spełniające kryteria przekazane w [SubmitInvoicesRequest](#submitinvoicesrequest).
   
* _invoices_ <= zawsze \
Lista metadanych pobranych faktur. Szczegółowy opis - por. dokumentacja API, [Pobranie listy metadanych faktur](https://ksef-test.mf.gov.pl/docs/v2/index.html#tag/Pobieranie-faktur/paths/~1api~1v2~1invoices~1query~1metadata/post).

Przykład:
```json
{
    "isTruncated": false,
    "invoices": 
    [
        {
            "ksefNumber": "5318308817-20251008-010020010448-AF",
            "invoiceNumber": "4/FVT/25/10",
            "issueDate": "2025-01-07T00:00:00+01:00",
            "invoicingDate": "2025-10-08T10:28:49.883+00:00",
            "acquisitionDate": "2025-10-08T10:29:16.937+00:00",
            "permanentStorageDate": "2025-10-08T10:29:59.159038+00:00",
            "seller": {
                "nip": "5318308817",
                "name": "XXXXXXXX-XXXXXXXX XX. X X.X."
            },
            "buyer": {
                "identifier": {
                    "type": "VatUe",
                    "value": "PL999999999"
                },
                "name": "EFG GmbH"
            },
            "netAmount": 444.6,
            "grossAmount": 444.6,
            "vatAmount": 0,
            "currency": "PLN",
            "invoicingMode": "Offline",
            "invoiceType": "Vat",
            "formCode": {
                "systemCode": "FA (3)",
                "schemaVersion": "1-0E",
                "value": "FA "
            },
            "isSelfInvoicing": false,
            "hasAttachment": false,
            "invoiceHash": "yo2vMMYNvZs+DamRkZiAnnCDNfbD2D5ZZfiMXrVswFg=",
            "thirdSubjects": [
                {
                    "identifier": {
                        "type": "Nip",
                        "value": "9999999999"
                    },
                    "name": "XXXX X.X.",
                    "role": 2
                },
                {
                    "identifier": {
                        "type": "Nip",
                        "value": "9999999999"
                    },
                    "name": "XXXX X.X. XXXXXXXXXXX XXXXXXXX XXXXX. XXXXXX. X.X.",
                    "role": 10
                }
            ]
        },

		.
		.
		.

        {
            "ksefNumber": "5318308817-20251008-010040DE3646-65",
            "invoiceNumber": "3/FVT/25/10",
            "issueDate": "2025-01-07T00:00:00+01:00",
            "invoicingDate": "2025-10-08T10:13:06.885+00:00",
            "acquisitionDate": "2025-10-08T10:13:32.53+00:00",
            "permanentStorageDate": "2025-10-08T10:14:05.267674+00:00",
            "seller": {
                "nip": "5318308817",
                "name": "XXXXXXXX-XXXXXXXX XX. X X.X."
            },
            "buyer": {
                "identifier": {
                    "type": "VatUe",
                    "value": "PL999999999"
                },
                "name": "EFG GmbH"
            },
            "netAmount": 444.6,
            "grossAmount": 444.6,
            "vatAmount": 0,
            "currency": "PLN",
            "invoicingMode": "Offline",
            "invoiceType": "Vat",
            "formCode": {
                "systemCode": "FA (3)",
                "schemaVersion": "1-0E",
                "value": "FA "
            },
            "isSelfInvoicing": false,
            "hasAttachment": false,
            "invoiceHash": "DzEHXHiYYY0/BWZEK8Ov+o3EHdBUxOroaCVrcDuQcok=",
            "thirdSubjects": [
                {
                    "identifier": {
                        "type": "Nip",
                        "value": "9999999999"
                    },
                    "name": "XXXX X.X.",
                    "role": 2
                },
                {
                    "identifier": {
                        "type": "Nip",
                        "value": "9999999999"
                    },
                    "name": "XXXX X.X. XXXXXXXXXXX XXXXXXXX XXXXX. XXXXXX. X.X.",
                    "role": 10
                }
            ]
        }
    ]
}
```


### CreateKsefCsr
Tworzy i zwraca nowy wniosek (CSR) o certyfikat KSeF, oraz powiązany z nim klucz prywatny.

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* *encryption*  
  typ tworzonego klucza. Domyślnie to "RSA", ale możesz też wpisać "ECDsa" (tzw. "klucz eliptyczny"). Wpisuj te symbole zachowując podaną tu konwencję dużych i małych liter!

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
   "encryption": "ECDsa",
   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbi10eXBlIjoiT3BlcmF0aW9uVG9rZW4iLCJvcGVyYXRpb24tcmVmZXJlbmNlLW51bWJlciI6IjIwMjUwNTE0LUFVLTJERkM0NkMwMDAtM0FDNkQ1ODc3Ri1ENCIsImV4cCI6MTc0NzIzMTcxOSwiaWF0IjoxNzQ3MjI5MDE5LCJpc3MiOiJrc2VmLWFwaS10aSIsImF1ZCI6ImtzZWYtYXBpLXRpIn0.rtRcV2mR9SiuJwpQaQHsbAXvvVsdNKG4DJsdiJctIeU",
}
```
ZWRACA:

* _base64Csr_ <= zawsze \
CSR, gotowy do wysłania na serwer (DER enkodowany w Base64)
   
* _base64Key_ <= zawsze \
klucz prywatny, enkodowany w Base64

Przykład:
```json
{
	"base64Csr":"MIIBFDCBvAIBADBaMQ8wDQYDVQQDDAZaaWdaYWsxHzAdBgNVBAoMFlppZ1phayBXaXRvbGQgSmF3b3Jza2kxGTAXBgNVBGEMEFZBVFBMLTUyNTE0NjkyODYxCzAJBgNVBAYTAlBMMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE+eYlLatHowgivulnLCEyhST0xmxWYn\/NKUyjao3zKznIivNFaesqS0f0uarVQI\/0jWiVQRhrIWC98fSFugPF2aAAMAoGCCqGSM49BAMCA0cAMEQCIDn9k\/bg0eXc0oOr8J6LTCjN5zfkq99kGq0i2SJmp02OAiA2PUUr3pzg3HdwK8PNQYmf1eKMHFuP99F24o5Bbr4JAg==",
	"base64Key":"MHcCAQEEIPc0n\/SVwSHzcrnGNSmZYVaDLolZne0xYQO8xn9YzYuboAoGCCqGSM49AwEHoUQDQgAE+eYlLatHowgivulnLCEyhST0xmxWYn\/NKUyjao3zKznIivNFaesqS0f0uarVQI\/0jWiVQRhrIWC98fSFugPF2Q=="
}
```
>[!NOTE]
>Certyfikat KSeF jest kopią certyfikatu, którego użyto do autoryzacji aktualnej sesji. Dlatego ta metoda nie potrzebuje żadnych argumentów, poza typem klucza i tokenem dostępowym.

### CompleteKsefCertificate
Pobiera certyfikat z KSeF i łączy go z podanym kluczem prywatnym

_[=> spis treści](#spis-tresci)_

ARGUMENTY:

* _serialNumber_ <= zawsze \
Numer seryjny certyfikatu.

* _base64Key_ <= zawsze \
klucz prywatny, enkodowany w Base64 (otrzymany od [CreateKsefCsr](#createksefcsr)).

* *encryption*  <= zawsze \
  typ dostarczonego klucza.  "RSA" lub "ECDsa". Wpisuj te symbole zachowując podaną tu konwencję dużych i małych liter!

* *saveTo* \
  ścieżka do pliku, w którym ma zostać zapisany utworzony certyfikat wraz z kluczem prywatnym. Na przykład: "*../certs/seal.pem*". Rozszerzenie nazwy pliku określa format, w którym certyfikat ma być zapisany. Możliwe są dwa: ".pem" lub ".pfx". Jeżeli podasz jakikolwiek inny, program zapisze wynik do pliku *\*.pem*.

* _accessToken_ <= zawsze \
Aktualny token dostępowy.

Przykład:
```json
{
	"serialNumber":"018B825ED6DF7913",
	"encryption":"RSA",
	"base64Key":"MIIEowIBAAKCAQ (...)9g+FmFcrQ8TP1",
	"accessToken":"eyJhbGciOiJIU(...)9KbPRRo3U"
}
```
REZULTAT:

* _certificatePem_ <= zawsze \
Tekst certyfikatu w formacie PEM.

* *certFile*  \
  Pełna ścieżka do pliku z certyfikatem. To proste rozwinięcie argumentu *saveTo*.

```json
{
	"certificatePem":"-----BEGIN CERTIFICATE-----  ...  -----END PRIVATE KEY-----"
}
```

### (pozostałe)
Możesz wywołać dowolny enpoint KSeF, przesyłając jako żądanie jego url wraz z ewentualnymi parametrami (bez nazwy serwera). Temu żądaniu muszą towarzyszyć wyliczne poniżej argumenty:

_[=> spis treści](#spis-tresci)_

ARGUMENTY: 

* _method_ \
Metoda (HTTP) żądania REST: "GET", "POST", "DELETE"... Domyślnie: "POST"

* _body_ \
Dane przesyłane metodą "POST". JSON lub XML. W przypadku JSON - zamiast cudzysłowów (") stosuj apostrofy (').

* _headers_ \
Opcjonalny słownik ("nazwa":"wartość") dodatkowych nagłówków HTTP

* _accessToken_ \
Aktualny token dostępowy (dla niektórych ządań nie jest potrzebny)

Przykład: dane przesyłane żądaniem POST "*/api/v2/invoices/query/metadata*":
```json
{
	"method":"POST",
	"body":"{ 'subjectType':'Subject2', 'dateRange':{ 'dateType':'PermanentStorage', 'from':'2025-10-01T00:00:00+02:00'} }",
	"accessToken":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0eXAiOiJDb250ZXh0VG9rZW4iLCJjaXQiOiJOaXAiLCJjaXYiOiI1MjUxNDY5Mjg2IiwiYXVtIjoiUXVhbGlmaWVkU2VhbCIsImFybiI6IjIwMjUwOTMwLUFVLTQwMUZCODUwMDAtMkNBNEMwNzM0MS1EQyIsInN1ZCI6IntcInN1YmplY3RJZGVudGlmaWVyXCI6e1widHlwZVwiOlwiTmlwXCIsXCJ2YWx1ZVwiOlwiNTI1MTQ2OTI4NlwifSxcImNvbW1vbk5hbWVcIjpcIlppZ1pha1wiLFwiY291bnRyeU5hbWVcIjpcIlBMXCIsXCJvcmdhbml6YXRpb25OYW1lXCI6XCJaaWdaYWsgV2l0b2xkIEphd29yc2tpXCIsXCJvcmdhbml6YXRpb25JZGVudGlmaWVyXCI6XCJWQVRQTC01MjUxNDY5Mjg2XCJ9IiwicGVyIjoiW1wiT3duZXJcIl0iLCJwZWMiOiJbXSIsInJvbCI6IltdIiwicGVwIjoiW10iLCJpb3AiOiJbXSIsImV4cCI6MTc1OTg1MDg2MSwiaWF0IjoxNzU5ODQ5OTYxLCJpc3MiOiJrc2VmLWFwaS10ZSIsImF1ZCI6ImtzZWYtYXBpLXRlIn0.SwjB8E8SEATyRob3MG2H7JQxh2uCGylnqT5Un0xy-Ic"
}
```

REZULTAT: \
Zależy od wywołanego enpointu. Na przykład, poniżej przedstawiam rezultat żądania "*/api/v2/certificates/limits*":
```json
{
	"canRequest":true,
	"enrollment":{"remaining":300,"limit":300},
	"certificate":{"remaining":100,"limit":100}
}
```

## Polecenia serwera ToDo
Serwer "ToDo" (*TargetUrl*=**"[https://jsonplaceholder.typicode.com](https://jsonplaceholder.typicode.com)"**) to umieszczony w Internecie serwer REST, oferujący coś w rodzaju rozbudowanej usługi "echo". W odróżnieniu od serwera testowego KSeF nie ma żadnych limitów wywołań, a jego polecenia nie wymagają autoryzacji. Przydaje się do testów w początkowych fazach pisania programu Klienta, gdy trzeba sprawdzić wywoływanie metod, przekazywanie argumentów, i inne szczegóły.  

### /posts
Edycja pojedynczego elementu typu "post" (tak naprawdę to niczego na serwerze ToDo nie zmienia, czysta demonstracja - por. [tutaj](https://jsonplaceholder.typicode.com/guide/)).

_[=> spis treści](#spis-tresci)_

ARGUMENTY: \
* _method_ \
Jedna z metod HTTP. Znaczenie: 
    - "GET" - odczytanie posta o numerze _id_.
	- "POST" - stworzenie nowego posta. Jego zawartość umieść w polu _data_.
	- "PUT" - modyfikacja istniejącego posta o numerze _id_. Jego nową zawartość umieść w polu _data_.
	- "PATCH" - modyfikacja fragmentu posta o numerze _id_. Modyfikowany fragment umieść w _data_.
	- "DELETE" - "usunięcie" posta o numerze _id_.
* _id_ \
identyfikator posta (numer od 1 do 100). Pomiń, gdy _method_ = "POST".
* _data_ \
  Dane przesyłane na serwer jako zawartość (JSON), np:
```
"{
	"id"     : 70,
	"title"  : "First impressions...",
	"body"   : "First impressions are usually misleading, but I wouldn not worry too much about it.", 
	"userId" : 1 
}"
```
REZULTAT: \
Zazwyczaj JSON ze zawartością "stworzonego" / "zmienionego" posta, np.:
```
{
  "title": "First impressions...",
  "body": "First impressions are usually misleading, but I wouldn not worry too much about it.",
  "userId": 1,
  "id": 101
}
```
Wyjątkiem jest metoda "DELETE", która zwraca pustą strukturę ("{}")
UWAGI: \
* Dla niektórych metod, użycie id=101 powoduje ciekway błąd serwera ToDo.

### (pozostałe)
Gdy wywołasz dowolne inne polecenie udostępniane przez serwer ToDo (np. "/users/1", albo "/comments"), to zostanie ono zrealizowane jako żądanie HTTP "GET":

_[=> spis treści](#spis-tresci)_

ARGUMENTY: brak

REZULTAT: \
różne struktury udostępniane przez ToDo. Na przykład, poniżej przedstawiam rezultat żądania "/users/1":
```json
{
  "id": 1,
  "name": "Leanne Graham",
  "username": "Bret",
  "email": "Sincere@april.biz",
  "address": {
    "street": "Kulas Light",
    "suite": "Apt. 556",
    "city": "Gwenborough",
    "zipcode": "92998-3874",
    "geo": {
      "lat": "-37.3159",
      "lng": "81.1496"
    }
  },
  "phone": "1-770-736-8031 x56442",
  "website": "hildegard.org",
  "company": {
    "name": "Romaguera-Crona",
    "catchPhrase": "Multi-layered client-server neural-net",
    "bs": "harness real-time e-markets"
  }
}
```

<!-- W Visual Studio wersję zmieniasz we właściwościach projektu Ksef.Services, u samego dołu sekcji Package/General (szybciej skoczyć do /License i przewinąć ekran w górę) -->

# Lista zmian w programie 

## wersja 1.0.0.0
2025-12-25
* Kompilacja z wersją 2.00 biblioteki MF (KSeF.Client.Core, KSeF.Client).

## wersja 0.9.0.5
2025-12-06
* Adaptacja kodu do RC 6.01 biblioteki MF (KSeF.Client.Core, KSeF.Client).

## wersja 0.9.0.0
2025-11-24
* Dodane i przetestowane polecenia pobraniu (eksportu z KSeF) paczki faktur: SubmitInvoicesRequest, GetInvoicesRequestStatus, DownloadInvoices.
* Dodane polecenie #WindowState, sterujące trybem wyświetlania okna konsoli KSeF.Services. (Okazało się potrzebne, by przy okazji wywoływania #XadesSign dla podpisu z magazynu Windows na chwilę zmienić stan okna do _Normal_. Tylko w takim stanie ewentualne systemowe okno dialogowe na wpianie PIN użytego certyfikatu pojawia się na pierwszym planie). 
* Usunięte różne zauważone błędy.

## wersja 0.8.0.0
2025-11-06
* Dodane nowe polecenia lokalne: #CertificateToPem, #CertificateFromPem. 
* Różne drobne poprawki i usprawnienia do istniejących metod, związane z dodaną obsługą certyfikatów z zaszyfrowanymi hasłami. (Dostosowanie do formatu certyfikatów uzyskiwanych z Aplikacji Podatnika).
* Adaptacja do aktualnej biblioteki MF (m.in. zarejestrowanie w konfiguracji kryptografii klasy odpowiedzialnej za podpisywanie plików XML kluczem ECDsa).

## wersja 0.7.0.0
2025-10-24
* Dodane nowe polecenia lokalne: #GetTokenProperties, #GetCertificateProperties, #ListStoreCertificates.
* Dodano polecenia pobierania faktur z repozytorium KSeF (w KSeF nazywa się to "eksportem").
* Dodano obsługę wysyłki wsadowej
* Dodano domyślną obsługę żądań, zwracającą rezultat dowolnego żądania REST wysłanego do KSeF. (Kolejne wyspecjalizowane żądania będę dodawać, gdy będa wymagać jakiegoś dodatkowego przetworzenia ppo stronie programu).
* Dodałem dwa nowe żądania związane z procesem obsługi wniosku o certyfikat KSeF.
* Wszystkie metody są przetestowane

## wersja 0.6.0.0
2025-10-04
* Dodane polecenie *#GetEncryptionData*
* Przetestowane polecenia wysyłania faktur sprzedaży.
* Różne inne drobne poprawki

## wersja 0.5.0.0
2025-10-01
* Dodane polecenia odczytywania i pobierania faktur (jeszcze nie przetestowane)
* Przetestowane polecenia autoryzacji
* Przystosowanie do RC 5.1 biblioteki MF.

## wersja 0.4.0.0
2025-09-19 
* Dodane polecenia uwierzytlenienia API KSeF i wysyłania faktur sprzedaży (jeszcze nie przetestowane). 
* Drobne poprawki w argumentach programu (serwer KSeF można wskazać za pomocą nazwy symbolicznej: TEST, DEMO, PROD). 

## wersja 0.3.0.0
2025-08-19 
* Dodatkowe polecenia tworzące testowy (*self-signed*) certyfikat osoby fizycznej (podpis cyfrowy) i prawnej (pieczęć firmowa). Różnią się polami Subject. Każdy z nich można generować z kluczem RSA lub ECDsa. 
* Opracowanie i przetestowanie poleceń generujących linki i kody QR dla trybu online i offline. (Na razie brak możliwości sprawdzenia z serwerem). 

## wersja 0.2.0.0
2025-07-31 
* Ustalenie wewnętrznych interfejsów do implementacji poleceń (*IRequestHandler*, *IHandlerProvider*) i metodologii ich implementacji.
* Opracowanie i przetestowanie pierwszych poleceń lokalnych związanych z szyfrowaniem, enkodowaniem Base64, metadanymi pliku, podpisem XAdES. 
## wersja 0.1.0.0
2025-07-15 \
Pierwsza wersja: 
* Stworzony podstawowy "szkielet" programu, bez żadnych poleceń za wyjątkiem **END**. 
* Sprawdzona komunikacja z Klientem, konfiguracja, dzienniki, oraz komunikacja REST za pomocą interfejsu IRestClient z biblioteki .NET KSeF. 
* Przeprowadzono testy prostych poleceń REST w komunikacji z serwerem "ToDo" (https://jsonplaceholder.typicode.com).

.
[^1]: Technicznie - to nie serwis Windows, a zwykły program linii poleceń (*console application*).
[^2]: Do **Ksef.Services.exe** stosują się wszystkie standardowe mechanizmy konfiguracji aplikacji .NET. Najpierw program szuka pliku konfiguracyjnego o nazwie **appsettings.json** w <u>**aktualnym katalogu**</u>. To może nie być katalog z *Ksef.Services.exe*, a np. katalog domyślny aplikacji Klienta. Np. w przypadku plików Excela to *%userprofile%\Documents*. Dodatkowo, w tym samym folderze można mieć kilka wersji pliku konfiguracji. Można wybierać je poprzez zmianę wartości zmiennej środowiskowej (użytkownika) o nazwie **DOTNET_ENVIRONMENT**. Gdy np. jej wartością jest "Test", to aplikacja załaduje ustawienia z pliku *appsettings.Test.json*. Potem te ustawienia są "nadpisywane" przez ewentualny plik określony w argumencie *--Settings* (*KSeF.Services.json*), a te - przez pozostałe wartości argumentów z linii poleceń programu. 
[^3]: W tej sekcji stosuję konwencję numeracji znaków w tekście używaną w Visual Basic For Applications: pierwszy znak w ciągu to znak nr 1.
[^4]: Potok działa jak kolejka: serwer po wpisaniu tekstu nie może już go w żaden sposób wycofać. Dlatego pierwsza odczytana linia mogła zostać wpisana dawno temu, a od tego czasu stan programu mógł ulec zmianie. Klient powinien odrzucać wszystkie linie stanu programu z sygnaturą czasową wcześniejszą niż chwila, w której rozpoczął wczytywanie. (Por. implementacja metody **Inquiry** z modułu *KsefServices* przykładowego Klienta z Excela).
