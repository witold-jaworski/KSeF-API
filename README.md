# KSeF Services
**KSeF.Services** jest działająca "w tle" lokalną aplikacją (*background service*) Microsoft Windows. To "wrapper" wokół biblioteki udostępnionej przez Ministerstwo Finansów. Pozwala połączonemu z nim programowi wywoływać metody API KSeF i zwraca rezultaty tych metod. Wartość dodana tego narzędzia: wykonuje wymagane przez API KSeF zaawansowane operacje na danych wejściowych, związane z szyfrowaniem, liczeniem skrótów, czy infrastrukturą PKI. To umożliwia implementację obsługi KSeF w  starszych językach programowania lub skryptach (np. JScript, VBA). 

**KSeF.Services.exe** jest programem lini poleceń napisanym w .NET 9.0. Powstał z szablonu _Worker Service_, towarzyszącemu SDK dla Windows. Jest przeznaczony do działania na tym samym komputerze, co program Klienta. Komunikuje się z nim poprzez potoki nazwane.   

>[!IMPORTANT]
>Aby skompilować ten projekt, należy dodatkowo pobrać [oficjalną bibliotekę CIRFMF .NET dla KSeF](https://github.com/CIRFMF/ksef-client-csharp). Umieść folder z jej plikiem _*.sln_ (ksef-client-csharp) obok folderu z tym rozwiązaniem:
```
KSeF-API\ <=to folder tego rozwiązania 
ksef-client-csharp\ <= to folder biblioteki MF 
```
Następnie otwórz w Visual Studio rozwiązanie _KSeF-API.sln_ i dodaj poleceniem **Add:Existing Project...** dwa projekty z folderu _ksef-client-csharp_: **KSeF.Client** i **KSeF.Client.Core**. (Figurują w zależnościach projektu _KSeF.Services_). 
>[!NOTE]
>Aby skompilować projekt _KSeF.Client_, w sekcji _Build:Strong naming_ jego właściwości wyłącz opcję podpisywania kodu (_Sign the assembly_).

## Uwagi i linki do dokumentacji
Publikuję tu wersję aplikacji, z której sam korzystam. Zestaw udostępnionych przez nią metod to odzwierciedlenie potrzeb mojego Klienta. Sądzę jednak, że jest na tyle szeroki, że może się przydać innym. W razie potrzeby stwórz swoją wersję (_fork_) tego projektu. 

Dodawanie kolejnych klas do obsługi nowych żądań jest proste, opisałem je [tutaj](KSeF.Services/docs/Rozbudowa.md)

Szczegóły użycia Ksef.Services.exe / implementacji Klienta znajdziesz w [opisie programu](KSeF.Services/docs/Opis.md)
