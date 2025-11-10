# KSeF Services
**KSeF.Services** to działająca "w tle" lokalna aplikacja (*background service*) Microsoft Windows. Jest adaptacją ("wrapperem") oficjalnej biblioteki udostępnionej przez Ministerstwo Finansów. KSeF.Services pozwala połączonemu z nią programowi wywoływać metody API KSeF, oraz oferuje trochę funkcji dodatkowych. Wartość dodana tego narzędzia: wykonuje wymagane przez API KSeF zaawansowane operacje na danych wejściowych, związane z szyfrowaniem, liczeniem skrótów, czy infrastrukturą PKI. To umożliwia implementację obsługi KSeF w  starszych językach programowania lub skryptach (np. JScript, VBA). 

**KSeF.Services.exe** jest programem lini poleceń napisanym w .NET 9.0. Powstał z szablonu _Worker Service_, towarzyszącemu SDK dla Windows. Jest przeznaczony do działania na tym samym komputerze, co program Klienta. Komunikuje się z nim poprzez potoki nazwane.   

>[!IMPORTANT]
>Aby skompilować ten projekt, należy dodatkowo pobrać [oficjalną bibliotekę CIRFMF .NET dla KSeF](https://github.com/CIRFMF/ksef-client-csharp). Umieść jej folder (ksef-client-csharp) obok folderu tego rozwiązania[^1]:
```
KSeF-API\ <=to folder tego rozwiązania 
ksef-client-csharp\ <= to folder biblioteki MF 
```
Następnie otwórz w Visual Studio _KSeF-API\KSeF-API.sln_ i dodaj poleceniem **Add:Existing Project...** dwa projekty z folderu _ksef-client-csharp_: **KSeF.Client** i **KSeF.Client.Core**. (Figurują w zależnościach projektu _KSeF.Services_). 
>[!NOTE]
>Aby skompilować projekt _KSeF.Client_, w sekcji _Build:Strong naming_ jego właściwości wyłącz opcję podpisywania kodu (_Sign the assembly_).

## Uwagi i linki do dokumentacji
Publikuję tu wersję aplikacji, z której sam korzystam. Zestaw udostępnionych przez nią metod to odzwierciedlenie potrzeb mojego Klienta. Jest jednak na tyle szeroki, że może się przydać innym. W razie potrzeby stwórz swoją wersję (_fork_) tego projektu. 

Dodawanie klasy obsługującej jakieś nowe żądanie jest proste, opisałem je [tutaj](KSeF.Services/docs/Rozbudowa.md).

Szczegóły użycia Ksef.Services.exe / implementacji Klienta znajdziesz w [opisie programu](KSeF.Services/docs/Opis.md).\
Początek [tej sekcji](https://github.com/witold-jaworski/KSeF-API/blob/master/KSeF.Services/docs/Opis.md#informacje-zwracane-przez-potok-diagnostyczny) wyjaśnia architekturę aplikacji. 

[^1]:Słowo "rozwiązanie" w tym tekście oznacza termin _solution_ używany w Visual Studio. Jest związane z plikiem _*.sln_ umieszczonym w katalogu głównym każdego z tych dwóch folderów.
