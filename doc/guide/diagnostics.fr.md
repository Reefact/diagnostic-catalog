# Les diagnostics `DCAT`

🌍 **Langues :**  
🇬🇧 [English](./diagnostics.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque a vu passer un `DCATxxxx` et veut savoir ce qu'il signifie. Chaque diagnostic que
`DiagnosticCatalog.Analyzers` signale : ce qui le déclenche, pourquoi il existe, comment le
configurer.

Cet assemblage est livré dans le paquet `DiagnosticCatalog` plutôt que dans un paquet à lui : il n'y
a donc rien à référencer pour les obtenir. Chaque catalogue dépend de la fondation et n'a pas le
droit de la masquer, si bien que référencer n'importe quel catalogue les active
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)) ; référencer
`DiagnosticCatalog` seul est la façon d'être vérifié sans aucun catalogue.

Ils se répartissent en deux groupes. Les diagnostics de **déclaration** regardent une règle que vous
avez déclarée ; vous ne les voyez que si vous écrivez un catalogue. Les diagnostics de **site
d'utilisation** regardent une suppression que vous avez écrite, ce qui concerne la plupart des gens.

| Identifiant | Regarde | Titre | Défaut | Correctif |
| --- | --- | --- | --- | --- |
| [`DCAT0001`](#dcat0001) | site d'utilisation | `Category` et `Id` doivent référencer la même règle | **Erreur** | deux, non classés |
| [`DCAT0002`](#dcat0002) | déclaration | Une règle doit être déclarée comme classe statique non générique | Avertissement | oui, sous condition |
| [`DCAT0003`](#dcat0003) | déclaration | Une règle doit exposer une constante `string` publique nommée `Id` | Avertissement | oui, sous condition |
| [`DCAT0004`](#dcat0004) | déclaration | Une règle doit exposer une constante `string` publique nommée `Category` | Avertissement | oui, sous condition |
| [`DCAT0005`](#dcat0005) | déclaration | Le nom du type de règle devrait correspondre à son `Id` | Info | — |
| [`DCAT0006`](#dcat0006) | site d'utilisation | Utiliser une référence de catalogue plutôt que des littéraux | **Erreur** | oui |
| [`DCAT0007`](#dcat0007) | site d'utilisation | La suppression mêle une référence de catalogue et un littéral | **Erreur** | oui, sous condition |
| [`DCAT0009`](#dcat0009) | site d'utilisation | `UnconditionalSuppressMessage` n'accepte que les identifiants `IL####` | Avertissement | — |
| [`DCAT0011`](#dcat0011) | déclaration | La catégorie d'une règle doit référencer une constante de catégorie déclarée | Avertissement | — |
| [`DCAT0012`](#dcat0012) | déclaration | Un identifiant de règle devrait s'écrire `nameof` | Avertissement | oui, sous condition |
| [`DCAT0013`](#dcat0013) | déclaration | Le nom du type de règle ne dit pas son `Id` | Avertissement | — |
| [`DCAT0014`](#dcat0014) | site d'utilisation | Une suppression doit porter une justification | Avertissement | — |

`DCAT0008` et `DCAT0010` sont spécifiés mais délibérément hors de la 1.0.

---

## Diagnostics de site d'utilisation

### `DCAT0001`

**La catégorie et l'identifiant viennent de deux règles différentes.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S2094.Id)]
//               ^^^^^ de S1144           ^^^^^ de S2094
```

Copier-coller, presque toujours : vous avez dupliqué une suppression qui marchait et changé une
moitié.

C'est signalé **même quand les deux règles partagent une catégorie**, et ce cas est celui qui mérite
d'être compris. La ligne compile exactement vers la même chose qu'une suppression correcte, et
fonctionne parfaitement — jusqu'à ce que l'éditeur recatégorise l'une des deux règles, moment auquel
elle porte silencieusement la mauvaise catégorie sans que rien dans la plateforme ne le dise. Une
vérification qui comparerait des valeurs plutôt que des règles manquerait précisément ce cas.

**Deux correctifs, aucun recommandé.** Vous seul savez quelle moitié était la faute de frappe :

```text
Use SonarRule.S1144.Id        — garder la catégorie, corriger l'identifiant
Use SonarRule.S2094.Category  — garder l'identifiant, corriger la catégorie
```

Bon à savoir pendant que vous choisissez : Roslyn apparie une suppression sur **l'identifiant seul**
et ne consulte jamais la catégorie. Corriger la catégorie laisse donc ce qui est supprimé exactement
en l'état, là où corriger l'identifiant le change.

**L'autre faute sous cet identifiant : un membre au mauvais emplacement.** La même règle, les deux
membres référencés, et toujours rien de supprimé :

```csharp
[SuppressMessage(SonarRule.S1144.Id, SonarRule.S1144.Category)]   // intervertis
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.HelpLinkUri)]
```

Un type de règle porte plus que la paire : la complétion propose donc tous ses membres dans une même
liste. Les deux lignes compilent et résolvent ; d'après le paragraphe ci-dessus, c'est l'emplacement
de l'identifiant qui décide de ce qui est supprimé, et aucune des deux n'y met un identifiant.
**Aucun correctif n'est proposé ici** — savoir si vous avez écrit le mauvais membre ou la mauvaise
règle n'est pas à la portée d'un outil.

### `DCAT0006`

**Ces littéraux correspondent à une règle que votre projet voit.**

```csharp
[SuppressMessage("Major Code Smell", "S1144")]
```

Signalé uniquement quand une règle connue correspond à la paire : une base de code qui n'a adopté
aucun catalogue reste donc complètement silencieuse. Le correctif la réécrit en référence et ajoute
le `using` nécessaire.

L'identifiant est tronqué au premier deux-points avant l'appariement, exactement comme le fait
Roslyn, si bien que la forme générée par *Supprimer → Dans la source* de Visual Studio est reconnue :

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
```

Le suffixe est abandonné par le correctif. Il dupliquait le titre de la règle, que le catalogue porte
en documentation XML.

Si **deux** catalogues décrivent la même règle, vous obtenez le diagnostic et aucun correctif
automatique — choisir entre les deux vous revient.

> **Sur l'adoption.** Celui-ci se déclenche sur toutes les suppressions littérales d'un coup, le jour
> où vous ajoutez un catalogue — et le catalogue amène l'analyseur avec lui, aucune seconde
> référence ne s'interpose. C'est une **erreur** par défaut
> ([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.fr.md)) : le build qui ajoute le
> catalogue est donc le build qui casse. Descendez-le à `suggestion`, migrez avec *Corriger toutes
> les occurrences*, puis remontez-le.

### `DCAT0007`

**Une moitié migrée, une moitié encore littérale.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144")]
```

L'état à moitié fait le plus courant, et le seul où la règle visée est connue sans ambiguïté :
l'argument migré la nomme. Complété depuis cette règle, en ne réécrivant que le littéral — quelle
que soit l'écriture que vous avez choisie de l'autre côté, alias compris, elle est laissée
tranquille.

**Sauf si le littéral nomme autre chose.** `"S9999"` à côté de `SonarRule.S1144.Category` obtient le
diagnostic et **aucun** correctif, parce que le compléter ferait taire une règle différente de celle
qui est tue aujourd'hui — et laisserait revenir l'avertissement d'origine. C'est une décision, pas
une migration.

### `DCAT0009`

**Une règle non `IL` utilisée dans `UnconditionalSuppressMessage`.**

```csharp
[UnconditionalSuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

Cet attribut est lu par le *trimmer*, depuis votre assemblage compilé, bien après que le compilateur
a fini. Son décodeur n'accepte que les identifiants de la forme `IL####` et **jette purement et
simplement tout le reste**. Roslyn ne traite pas non plus cet attribut. Cette suppression est donc un
no-op qu'aucun autre outil de la chaîne ne signale.

La vérification reproduit le décodeur du *trimmer* plutôt qu'un motif plus strict : les identifiants
qu'il honore *effectivement* sont laissés tranquilles — y compris sa propre forme
`IL2026:FriendlyName`. Les signaler reviendrait à vous demander de changer quelque chose qui
fonctionne.

### `DCAT0014`

**La suppression nomme une règle et ne dit jamais pourquoi.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

Tout le reste de cette page porte sur *quel* diagnostic une ligne fait taire. Celui-ci porte sur
l'autre moitié. La paire est désormais vérifiée par le compilateur ; la raison pour laquelle
l'avertissement était acceptable n'est écrite nulle part, et elle ne se retrouve pas après coup —
l'avertissement a disparu, et la seule personne qui savait est celle qui a décidé qu'il n'importait
pas. Six mois plus tard, plus personne ne distingue une suppression réfléchie d'une suppression
copiée-collée.

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Appelé par le sérialiseur via la réflexion.")]
```

**La présence est tout le contrat.** La valeur est lue pour sa longueur, jamais pour son sens : une
justification d'un mot passe, et une que vous auriez mieux rédigée aussi. Juger ce qu'une
justification *dit* est délibérément hors périmètre et le reste — c'est une question humaine, et un
outil qui noterait de la prose se tromperait dans les deux sens.

Une seule valeur non vide est refusée : `"<Pending>"`, le marqueur que Visual Studio écrit quand il
génère une suppression pour vous. C'est le mot de cet outil pour *personne n'a encore rempli ceci*,
reconnu exactement et rien d'approchant — `"n/a"` et `"évident"` passent, parce que trancher sur
ceux-là serait lire de la prose. Une chaîne vide, des espaces et `Justification = null` sont vides et
signalés comme tels.

**Toute suppression y est tenue, y compris une suppression entièrement écrite en littéraux.** C'est
le seul diagnostic d'ici qui n'a besoin de rien du catalogue : une suppression littérale fait taire un
avertissement exactement comme une référence, et en dit exactement aussi peu sur le pourquoi.

```csharp
[SuppressMessage("Usage", "xUnit1004")]   // signalé, même sans le moindre catalogue en vue
```

Cette ligne compte plus qu'il n'y paraît. [`DCAT0006`](#dcat0006) ne signale une paire littérale que
si une règle visible de votre projet lui correspond ; une suppression nommant une règle qu'aucun
catalogue ne décrit n'était donc, avant celui-ci, signalée par rien du tout.
`UnconditionalSuppressMessage` y est tenu aussi — une suppression lue par un outil qui s'exécute bien
après le compilateur est celle qui a le plus besoin de dire pourquoi elle existe.

La seule forme laissée tranquille est un identifiant qui ne nomme rien,
`[SuppressMessage("Usage", null)]` : Roslyn fait la correspondance sur l'identifiant, cette ligne ne
fait donc rien taire et n'a rien à justifier.

Une ligne en cours de migration est donc signalée deux fois — `DCAT0006` pour la paire, celui-ci pour
la raison — et c'est délibéré : convertir une suppression ne répond pas à la question à laquelle elle
n'a jamais répondu. Si vous faites déjà tourner `SA1404` de StyleCop, vous verrez les deux ; elles
posent la même question, et une ligne d'`.editorconfig` fait taire celle dont vous ne voulez pas
([ADR-0039](../adr/0039-require-a-justification-on-every-suppression.fr.md)).

**Aucun correctif, et aucun n'est possible.** Ce qui doit y figurer est la seule chose de l'attribut
qui ne se lit pas dans le code
([ADR-0018](../adr/0018-a-code-fix-never-decides-what-only-the-author-can.fr.md)).

Il est livré en `Avertissement` et non en erreur, contrairement à ses trois voisins de site
d'utilisation : il signale des lignes par ailleurs entièrement correctes, et un projet qui a adopté un
catalogue avant que cette règle n'existe ne doit pas voir son build tomber du jour au lendemain à
cause d'elles. Une ligne d'`.editorconfig` la relève le jour où vous le voulez.

---

## Diagnostics de déclaration

Ceux-ci se déclenchent sur du code qui déclare des règles. Voir
[le guide de l'auteur de catalogue](authoring-a-catalogue.fr.md).

Ils se répartissent en deux groupes. `DCAT0002`, `DCAT0003`, `DCAT0004` et `DCAT0011` disent que la
règle est **inutilisable ou sans ancrage** ; `DCAT0005`, `DCAT0012` et `DCAT0013` disent qu'elle
fonctionne et que son nom ne dit pas ce qu'elle est.

Ceux qui proposent un correctif le proposent **quand la réparation est déjà écrite dans le code**, et
se taisent sinon. Cette ligne n'est pas de la prudence pour elle-même : un correctif qui devinerait
produirait une règle que le compilateur accepte et que personne ne vérifie, c'est-à-dire la
défaillance que cette bibliothèque existe pour éliminer. Là où un correctif est refusé ci-dessous, le
diagnostic nomme quand même le type et le membre — vous terminez avec ce que vous savez, et l'outil
s'en abstient.

`DCAT0011` n'en propose aucun, pour la même raison poussée d'un cran : la réparation est une classe
qui n'existe pas encore, portant une constante que personne n'a nommée.

### `DCAT0002`

**Marqué `[DiagnosticRule]` mais pas une classe statique non générique.** Une règle porte des
constantes et n'est jamais instanciée ; une règle générique n'a aucun membre constant à offrir.

**Correctif — *Make 'X' static*.** Proposé pour une classe ordinaire qui pourrait porter le mot-clé :
pas de paramètres de type, pas de type de base ni d'interface, aucun membre d'instance, aucun
constructeur d'instance, pas `partial`. Un `sealed` ou un `abstract` devenu redondant part avec, le
compilateur rejetant l'un comme l'autre à côté de `static`.

Rien n'est proposé pour un type générique ni pour un `struct`, une `interface`, une `enum` ou un
`record` — retirer les paramètres de type, ou changer la nature du type, n'est pas une réparation de
ce que vous avez écrit mais son remplacement. Une classe `partial` est refusée parce que les parties
que le correctif ne voit pas peuvent porter les membres d'instance qui tranchent la question.

### `DCAT0003`

**Pas de `const string Id` publique.** La cause habituelle est `static readonly` au lieu de `const` :
il a une valeur à l'exécution mais ne peut pas être un argument d'attribut, ce qui est tout l'objet.
Une valeur vide ou faite d'espaces compte comme absente.

Utilisez `nameof(LeTypeDeLaRègle)`, qui ne peut pas diverger du type qu'il nomme.

**Correctif — *Make 'Id' a public constant*.** Proposé quand le membre est là et que seuls ses
modificateurs sont mauvais : un champ `string` privé, `internal`, `static readonly`, ou autrement
non constant mais de valeur constante, devient `public const string` en une étape. Les deux défauts
d'un coup, délibérément — réparer l'accessibilité d'un `private static readonly` et s'arrêter là
laisserait l'avertissement sur le membre qu'on vient d'éditer.

**Correctif — *Declare 'public const string Id'*.** Proposé quand le membre est absent, et il écrit
`nameof(LeTypeDeLaRègle)`. C'est la forme recommandée plutôt qu'un espace réservé : elle est lue sur
la déclaration, et pour un catalogue dont les types portent le nom de leurs règles, c'est déjà la
bonne valeur.

Aucun des deux n'est proposé quand c'est la *valeur* qui est fausse — un `const int`, une chaîne
vide, un initialiseur non constant, ou une propriété plutôt qu'un champ. Le code ne dit rien de ce
que l'identifiant aurait dû être.

### `DCAT0004`

**Pas de `const string Category` publique.** Mêmes règles que pour `Id`.

Sa *valeur* devrait être celle que déclare le `DiagnosticDescriptor` de l'analyseur d'origine. Rien
dans la plateforme ne le vérifie — ce qui est exactement pourquoi la constante vaut la peine.

**Correctif — *Make 'Category' a public constant*.** Exactement comme pour `Id`.

**Correctif — *Declare 'public const string Category'*.** Écrit l'espace réservé `"TODO"`. Prenez ce
mot au pied de la lettre : la catégorie appartient à l'analyseur que cette règle reflète et le
correctif n'a aucun moyen de la connaître ; il échafaude donc le membre et vous laisse la valeur.

> **Ce que coûte l'espace réservé.** `"TODO"` est une chaîne non vide : `DCAT0004` cesse donc d'être
> signalé dès que vous appliquez le correctif. Ce qui le remplace est `DCAT0011` : l'espace réservé
> est écrit comme un littéral, le build vous demande donc maintenant de déclarer la catégorie là où
> votre catalogue déclare ses catégories. Le travail inachevé reste nommé — mais notez ce qu'aucune
> des deux règles ne peut voir, parce que Roslyn apparie une suppression sur son identifiant seul :
> une catégorie déclarée mais simplement *fausse* est invisible dans tous les builds, pour toujours.
> Appliquez-le quand vous êtes sur le point de le remplir, pas pour raccourcir la liste.

### `DCAT0005`

**L'identifiant ne peut pas être un nom de type : le type porte donc ce qui s'en approche le plus.**

```csharp
[DiagnosticRule]
public static class RULE_0001
{
    public const string Id = "RULE-0001";  // un tiret est légal dans un id, pas dans un nom de type
}
```

**Il n'y a rien à faire ici, et c'est tout le message.** `RULE_0001` et `RULE0001` rendent aussi
fidèlement `"RULE-0001"` l'un que l'autre, et cette bibliothèque n'a aucun titre à en élire un — elle
n'en réclame donc aucun, ne propose aucun correctif, et reste en `Info`, hors de votre sortie de build.

Pourquoi le signaler, alors ? Parce que [`DCAT0013`](#dcat0013) échoue à cette même comparaison une
étape plus loin et, lui, avertit. `DCAT0005`, c'est l'exception rendue visible : il marque les
déclarations où la divergence a été **subie** plutôt que choisie. Une exception que personne ne voit,
à l'intérieur d'une règle qui signale, est la seule forme sur laquelle un lecteur ne peut pas
raisonner — et elle ne vous laisserait aucun identifiant à hausser dans `.editorconfig` si vous
décidiez finalement de vouloir en être informé.

Un identifiant est lu jusqu'à son premier deux-points, exactement comme une suppression
([`DCAT0006`](#dcat0006)). La forme à nom convivial du *trimmer* atterrit donc ici plutôt que sous
`DCAT0013`, et un type nommé d'après sa tête fait tout ce qu'un nom peut faire :

```csharp
public static class IL2026Annotated
{
    public const string Id = "IL2026:Members annotated with RequiresUnreferencedCode";
}
```

### `DCAT0011`

**La catégorie n'est pas atteinte via une constante de catégorie déclarée.** `DCAT0004` demande si le
membre existe ; celui-ci demande d'où vient sa valeur. Elle doit se résoudre vers une `const string`
déclarée dans une classe marquée `[DiagnosticCategory]` :

```csharp
[DiagnosticCategory]
internal static class ContosoCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class CT0001
{
    public const string Id = nameof(CT0001);
    public const string Category = ContosoCategory.Usage;   // ← pas un littéral
}
```

Rien n'est cassé quand vous écrivez le littéral à la place. La règle compile, se replie sur la même
chaîne dans les métadonnées et supprime exactement ce qu'elle doit — c'est pourquoi ceci est livré en
`Avertissement` et non en erreur. Ce que cela coûte, c'est **une seule orthographe par catégorie** :
un catalogue répète très peu de valeurs distinctes sur un très grand nombre de règles, et chaque
transcription est un endroit où l'une d'elles peut dériver. Cela coûte aussi le marqueur, qui est ce
qui permet à l'outillage de distinguer une constante de catégorie de n'importe quelle autre constante
`string` de l'assembly — sans lui, aucun outil ne peut proposer la constante nommée en remplacement
d'un littéral.

Sont acceptées à égalité toutes les orthographes qui se lient au même champ : un nom qualifié, un
conteneur aliasé, un `using static`, un conteneur déclaré dans un autre assembly. Sont rejetées les
formes qui sont constantes sans être une référence unique — un littéral, `nameof(...)`, deux
constantes concaténées — parce qu'aucune ne laisse à la valeur une déclaration unique pour source.

**Aucun correctif n'est proposé.** La réparation est une classe qui n'existe peut-être pas encore,
portant une constante que personne n'a nommée ; un correctif qui inventerait les deux devinerait le
vocabulaire du catalogue. Le diagnostic nomme la règle et vous écrivez le conteneur.

**Signalé sur la source uniquement**, comme tout diagnostic de déclaration — et ici par construction
plutôt que par politique, puisque le contrôle lit l'initialiseur et qu'une règle qui vous parvient par
les métadonnées n'en a pas.
### `DCAT0012`

**L'identifiant est un littéral qui se trouve égaler le nom du type.** Écrivez `nameof` à la place :

```csharp
public const string Id = "JD0007";        // signalé
public const string Id = nameof(JD0007);  // solidaires
```

Rien n'est faux dans ce littéral aujourd'hui — c'est bien le problème. Il s'accorde avec le nom du
type *maintenant*, et rien ne l'y retient. Renommez le type et le littéral reste en arrière : la
déclaration compile toujours, et chaque site d'utilisation continue de nommer une règle que le type
n'est plus. `nameof` ne peut pas se disjoindre.

C'est le seul diagnostic de déclaration qui lit votre **source** plutôt que vos symboles.
`nameof(JD0007)` et `"JD0007"` compilent vers la même constante : une règle qui parvient à cet
analyseur depuis une assembly référencée ne porte aucune trace de ce qui a été écrit — et rien n'y est
signalé, puisqu'à ce stade il n'y a plus de forme à recommander.

Tout `nameof` compte, qualifié ou non : `nameof(Vendor.JD0007)` est tenu par le même opérateur.

**Correctif — *Use `nameof`*.** Proposé dès lors qu'`Id` est un champ à lui seul. Refusé quand une
déclaration de champ porte plusieurs constantes — `public const string Id = "JD0007", Category =
"Usage";` — car réécrire une déclaration partagée toucherait un membre que ce diagnostic n'a jamais
mentionné.

### `DCAT0013`

**Le type porte un nom que son identifiant ne dit pas.**

```csharp
[DiagnosticRule]
public static class RuleSeven
{
    public const string Id = "JD0007";  // signalé
}
```

`JD0007` est un nom de type parfaitement légal. Il était disponible, et le type s'appelle autrement :
chaque site d'utilisation lit donc `Vendor.RuleSeven.Id` et supprime `JD0007`. La référence compile,
se résout, fonctionne — et ne dit rien de vrai à qui la lit. C'est un défaut pire qu'une règle cassée,
laquelle au moins s'annonce.

Il est signalé dès que le nom ne dit pas l'identifiant sans que rien ne l'ait imposé. Les deux cas
suivants le sont, pour la même raison :

```csharp
public static class RULE001 { public const string Id = "RULE_001"; }   // RULE_001 était disponible
public static class RULE42  { public const string Id = "RULE-0001"; }  // n'en est pas une légalisation
```

Le second est celui qu'il faut connaître. `"RULE-0001"` ne peut pas être un nom de type du tout — mais
`RULE42` n'en est pas non plus un rendu, et ne pas pouvoir épeler l'identifiant exactement n'autorise
pas à en épeler un autre.

**Aucun correctif.** Deux réparations existent et vous seul pouvez trancher : renommer le type change
un nom que vos consommateurs ont écrit chez eux, et réécrire l'identifiant change quel diagnostic est
supprimé. Un outil qui en choisirait une déciderait laquelle des deux était la faute de frappe.

---

## Les configurer

Mécanismes Roslyn standards, aucun format propriétaire :

```ini
# .editorconfig
[*.cs]

# Une suppression que le trimmer jette. Pas une erreur par défaut uniquement
# parce que DCAT0009 rate encore un identifiant atteint via une constante.
dotnet_diagnostic.DCAT0009.severity = error

# Une suppression qui ne dit jamais pourquoi. Livrée en avertissement parce
# qu'elle signale des lignes par ailleurs correctes ; relevez-la quand toutes
# les vôtres portent une raison.
dotnet_diagnostic.DCAT0014.severity = error

# Déclarer des règles — vous n'en avez besoin que si vous publiez un catalogue.
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
dotnet_diagnostic.DCAT0011.severity = error
dotnet_diagnostic.DCAT0012.severity = error
dotnet_diagnostic.DCAT0013.severity = error

# Un nom qui n'aurait pas pu dire son id. Haussez-le si vous préférez revoir
# chacune de ces déclarations plutôt que de la laisser passer.
dotnet_diagnostic.DCAT0005.severity = warning

# Migrer un codebase existant : visible dans l'IDE, hors du build.
# Supprimez la ligne quand le dernier littéral a disparu.
dotnet_diagnostic.DCAT0006.severity = suggestion
```

`DCAT0001`, `DCAT0006` et `DCAT0007` sont déjà des erreurs, donc rien ci-dessus ne les
relève — le seul des trois à toucher est le dernier, et seulement le temps de migrer
([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.fr.md)).

La catégorie est `DiagnosticCatalog`, vous pouvez donc aussi les régler tous d'un coup :

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Cantonnez une section à un chemin de la façon ordinaire d'`.editorconfig` quand du code généré ou un
dossier hérité demande un traitement différent.

Cette même clé réglée sur `none` est la façon de tout **désactiver**. Puisque les analyseurs sont
livrés dans `DiagnosticCatalog`, il ne reste aucune référence de paquet à décliner : un projet qui
veut les marqueurs et aucune vérification le dit ici plutôt que dans ses dépendances
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)).

## Ce qui n'est délibérément pas vérifié

Les analyseurs vérifient qu'une suppression est **structurellement cohérente** — qu'elle nomme une
vraie règle, de façon cohérente. Ils ne font pas, et ne feront pas :

* valider une chaîne arbitraire. `[SuppressMessage("Usage", "S1144")]` avec une mauvaise catégorie ne
  correspond à aucune règle connue et n'est signalé par rien. Ce qui rend une mauvaise catégorie
  impossible, c'est la *constante*, que le compilateur vérifie — ces diagnostics vous amènent aux
  constantes et vous y maintiennent ;
* juger si supprimer une règle *à cet endroit* était raisonnable. `DCAT0014` exige qu'une
  `Justification` soit écrite et la lit pour sa seule longueur — ce qu'elle dit est pesé par des
  humains, jamais par ces analyseurs ;
* atteindre `#pragma warning disable` ou les clés de gravité d'`.editorconfig`, qui prennent du texte
  nu hors du modèle de compilation C#. Aucune constante ne peut jamais y être substituée.

---

<div align="center">
<a href="./adopting-a-catalogue.fr.md">← Adopter un catalogue sur une base de code existante</a> · <a href="./README.fr.md">↑ Table des matières</a>
</div>
