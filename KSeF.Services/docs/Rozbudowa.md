# Implementacja nowych metod KSeF.Sevices
Dodanie obs³ugi nowego ¿¹dania polega na dodaniu do kodu programu nowej klasy:
1. Dodaj do folderu **\Api** (lub **\Api\Local**) nowy plik klasy, o takiej nazwie jak symbol nowego ¿¹dania.
2. Dodaj do klasy atrybut **[HandlesRequest("<symbol ¿¹dania>")]**
3. Klasa musi implementowaæ interfejs **IRequestHandler**. Ka¿da z metod tego interfejsu mo¿e byæ wywo³ywana w ci¹gu "¿ycia" obiektu tylko raz, i to w takiej kolejnoœci, w jakiej wystêpuj¹ w interfejsie.

> [!TIP]
> Mo¿na stworzyæ klasy, które obs³uguj¹ kilka ró¿nych ¿¹dañ. Wtedy umieœæ nad nimi dodatkowe atrybuty *[HandlesRequest()]*, ka¿dy z symbolem dodatkowego ¿¹dania.

Klasê obs³ugi ¿¹dania mo¿na stworzyæ stworzyæ jako rozszerzenie abstrakcyjnej klasy **HandlerBase**. Ta klasa implementuje *IRequestHandler* i inicjalizuje wewnêtrzne pola obiektu dostêpne dla klasy potomnej:
* **_request**: tekst ¿¹dania (przydatny, gdy klasa obs³uguje wiêcej ni¿ jedno).
* **_ksefClient**: serwis *IKSeFClient*, do wywo³ania ¿¹dania.

Klasa _HandlerBase_ to jednoczeœnie "biblioteka" wielu u¿ytecznych metod, które obs³uguj¹ ró¿ne drobiazgi powtarzajace siê w implementacji ka¿dego handlera. \
W szczególnoœci udostêpnia w³aœciwoœci:
* **Scope**: serwis *IServiceProvider* zwi¹zany z aktywnym zakresem (*scope*) obs³ugi ¿¹dania *_request*. Jego metod¹ **GetRequiredService\<T\>()** mo¿esz pobieraæ potrzebne us³ugi (np. *ICryptographyService*).
* **Logger**: serwis *ILogger*. U¿ywaj go, by odnotowaæ coœ w logu programu.
 