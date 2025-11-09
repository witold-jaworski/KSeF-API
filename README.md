# KSeF Services
**KSeF.Services** jest "działająca w tle" aplikacją (*background service*) Microsoft Windows. To "wrapper" wokół biblioteki udostępnionej przez Ministerstwo Finansów. Pozwala korzystającym z niej programom wywoływać metody API KSeF i zwraca rezultaty tych metod. Jej wartość dodana: wykonuje wymagane przez to API zaawansowane operacje na danych wejściowych, związane z szyfrowaniem, liczeniem skrótów, czy infrastrukturą PKI. Korzystając tej aplikacji można implementować obsługę KSeF w  starszych językach programowania lub skryptach (np. JScript, VBA). 

**KSeF.Services.exe** jest programem Open Source, napisanym w .NET 9.0. Wykorzystuje [oficjalną bibliotekę CIRFMF .NET dla KSeF](https://github.com/CIRFMF/ksef-client-csharp). Należy ją także pobrać, bo figuruje w zależnościach tego projektu. program Klienta komunikuje się z KSeF.Services poprzez potoki nazwane. 

**Ważne**
Aby skompilować ten projekt, należy obok folderu z tym rozwiązaniem umieścić folder z biblioteką MF (ksef-client-csharp):
```
KSeF-API\ <=to folder tego rozwiązania
ksef-client-csharp\ <= to folder biblioteki MF
```
a następnie dodać do rozwiązania _KSeF-API.sln_ poleceniami **Add:Existing Project...** dwa projekty z folderu _ksef-client-csharp_: **KSeF.Client** i **KSeF.Client.Core**. (Figurują w zależnościach KSeF.Services)

## Uwagi i linki do dokumentacji
Publikuję tu wersję aplikacji, z której sam korzystam. Stąd zestaw udostępnionych przez nią metod to odzwierciedlenie potrzeb mojego Klienta. Sądzę jednak że jest na tyle szeroki, że nawet w tej postaci może się przydać innym. W razie potrzeby zawsze możesz stworzyć swój _fork_. 

Dodawanie kolejnych klas do obsługi nowych żądań jest proste, opisałem je [tutaj](KSeF.Services/docs/Rozbudowa.md)

Szczegóły użycia Ksef.Services.exe / implementacji Klienta znajdziesz w [opisie programu](KSeF.Services/docs/Opis.md)
