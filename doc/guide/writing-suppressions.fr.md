# Écrire des suppressions que le compilateur vérifie

🌍 **Langues :**  
🇬🇧 [English](./writing-suppressions.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque écrit `[SuppressMessage(...)]`. Aucune connaissance du fonctionnement interne de
DiagnosticCatalog n'est nécessaire pour lire cette page.

Un mot sur le vocabulaire : une **suppression** désigne ici le fait de faire taire un avertissement,
jamais le fait d'effacer du code. C'est le sens que `SuppressMessageAttribute` porte, et il est
conservé tel quel dans toute la documentation.

## Le problème, en un exemple

Vous avez un avertissement que vous avez décidé d'accepter, alors vous le faites taire :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Deux chaînes. Le compilateur n'en vérifie aucune, parce que de son point de vue ce n'est que du
texte. Si bien que tout ce qui suit compile, part en production, et ne fait strictement rien :

```csharp
// Chacune porte une raison : la seule chose fausse dans ces lignes est la paire.
[SuppressMessage("Major Code Smell", "S1145", Justification = "Réflexion.")]   // faute de frappe — un chiffre
[SuppressMessage("Major Code Smell", "S 1144", Justification = "Réflexion.")]  // espace parasite
[SuppressMessage("Major Code Smell", "S1144", Justification = "Réflexion.")]   // correct aujourd'hui ; retirée l'an prochain
```

Rien ne vous avertit. La suppression cesse simplement de correspondre, et l'avertissement qu'elle
masquait revient — ou, pire, ne revient jamais parce que le code a été effacé et que la suppression
morte reste là pour toujours. Ce n'est pas une erreur rare. C'est le résultat normal d'un
identifiant écrit à la main sans aucun retour.

## Ce que cette bibliothèque en fait

Elle remplace les deux chaînes par deux **constantes**, que le compilateur vérifie :

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Désormais :

* une faute d'orthographe donne une **erreur de compilation**, pas un no-op silencieux ;
* renommer dans l'IDE entraîne tous les sites d'utilisation ;
* la question « où cette règle est-elle supprimée ? » trouve sa réponse dans *Rechercher toutes les
  références*, puisqu'il s'agit bel et bien d'une référence ;
* quand l'éditeur retire la règle, la constante est marquée obsolète et vous êtes prévenu à la
  compilation.

La sortie compilée est **octet pour octet identique** à la version avec des littéraux. Les
constantes sont repliées à la compilation : cela ne coûte rien à votre application — ni dépendance,
ni vérification au démarrage, ni un seul octet. Voir
[l'empreinte nulle](#ce-que-ça-laisse-dans-mon-application) plus bas.

## Démarrer

### 1. Référencer un catalogue

Un catalogue est un paquet de constantes pour les règles d'un analyseur. Prenez celui qui correspond
à l'analyseur dont vous faites taire les avertissements :

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

**[L'index des catalogues](../README.fr.md#-les-catalogues-disponibles)** est la liste : une ligne
par analyseur, avec le préfixe de règles qu'il couvre. Elle n'est délibérément pas recopiée ici — une
copie partielle est la façon dont un lecteur conclut que son analyseur n'est pas couvert.

C'est la seule ligne dont vous avez besoin pour la garantie elle-même. Une règle mal orthographiée
est désormais une erreur de compilation, parce que `SonarRule.S1144.Id` est un membre que le
compilateur résout — aucun analyseur n'intervient là-dedans.

Les diagnostics `DCAT` ci-dessous viennent avec cette même ligne. Ils sont livrés dans
`DiagnosticCatalog`, dont chaque catalogue dépend et qu'aucun n'a le droit de masquer : la référence
ci-dessus est donc aussi ce qui trouve les suppressions que vous n'avez *pas* encore converties
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)). Si vous voulez les
vérifications sans catalogue, référencez `DiagnosticCatalog` seul. Et si vous publiez une
bibliothèque, il n'y a rien à arranger : les vérifications atteignent le projet qui a référencé le
catalogue et s'y arrêtent, si bien que qui vous référence n'est pas analysé par un catalogue qu'il
n'a jamais choisi
([ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md)).

### 2. Écrire la suppression

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer through reflection.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Chaque catalogue nomme son conteneur d'après son éditeur : `SonarRule`, `NetAnalyzersRule`,
`StyleCopRule`, `DcatRule`. Au singulier, parce que le site d'utilisation se lit `SonarRule.S1144` —
une règle, nommée.

### 3. Migrer l'existant

Vous n'avez pas à le faire à la main. Compilez une fois, et chaque suppression littérale qui
correspond à une règle de votre catalogue est signalée par `DCAT0006`, avec un correctif attaché.
Acceptez-le une fois, ou utilisez **Corriger toutes les occurrences** pour convertir un document, un
projet ou la solution entière en une étape.

Il gère la forme que Visual Studio génère, suffixe compris :

```csharp
[SuppressMessage(
    "Major Code Smell",
    "S1144:Unused private members should be removed",
    Justification = "Appelé par le sérialiseur.")]
// devient
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Appelé par le sérialiseur.")]
```

Le suffixe est abandonné. C'était de la prose reprenant le titre de la règle, que le catalogue porte
dans sa documentation XML — survolez la constante et vous le retrouvez.

## Les raccourcis, et celui qu'il faut éviter

Les noms de conteneur longs deviennent répétitifs. Un **alias** est la sortie recommandée :

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Unused.Category, Unused.Id, Justification = "...")]
```

Vérifié exactement comme la forme longue : l'analyse travaille sur les symboles, jamais sur le texte
que vous avez tapé.

`using static` fonctionne aussi mais n'est **pas** recommandé :

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Category, Id, Justification = "...")]   // correct — mais une seule règle par fichier
```

Un second `using static` dans le même fichier rend `Category` et `Id` ambigus, et la seule
correction est de revenir en arrière. L'alias passe à l'échelle ; celui-ci non.

## Ce dont on va vous parler, et pourquoi

Cinq diagnostics peuvent apparaître sur une suppression. Référence complète dans
[le guide des diagnostics](diagnostics.fr.md) ; voici ce que chacun signifie en pratique.

**`DCAT0001` — les deux arguments viennent de règles différentes.**

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S2094.Id,
    Justification = "Appelé par le sérialiseur.")]
```

Copier-coller, presque toujours. C'est signalé *même quand les deux règles partagent une catégorie*,
parce qu'alors la ligne fonctionne aujourd'hui et casse le jour où l'éditeur recatégorise l'une des
deux — le genre de défaut qui remonte des années plus tard sans le moindre indice attaché.

Deux correctifs sont proposés et aucun n'est marqué par défaut, parce que vous seul savez quelle
moitié était la faute de frappe. Bon à savoir pendant que vous choisissez : Roslyn apparie une
suppression sur **l'identifiant seul** et ne regarde jamais la catégorie. Corriger la catégorie ne
change donc rien à ce qui est supprimé, là où corriger l'identifiant le change.

**`DCAT0006` — ces littéraux correspondent à une règle que vous avez.** La migration ci-dessus.

**`DCAT0007` — vous n'en avez migré que la moitié.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144", Justification = "Appelé par le sérialiseur.")]
```

Complété depuis la règle que l'autre argument nomme déjà. Si le littéral nomme *autre chose* —
`"S9999"` à côté de `SonarRule.S1144.Category` — vous obtenez le diagnostic et **aucun** correctif,
parce que le compléter ferait taire une règle différente de celle qui est tue aujourd'hui. C'est
votre décision, pas celle d'une ampoule.

**`DCAT0009` — `UnconditionalSuppressMessage` avec une règle non `IL`.** Cet attribut est lu par le
*trimmer*, qui n'accepte que les identifiants `IL####` et jette tout le reste. La suppression que
vous avez écrite ne fait donc rien, et rien d'autre dans la chaîne d'outils ne vous l'aurait jamais
dit.

**`DCAT0014` — la suppression ne dit jamais pourquoi.**

<!-- dcat-doc:missing-justification le déclencheur de DCAT0014 ; la raison absente EST ce que ce bloc montre -->

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]   // incorrect : aucune Justification
```

Les quatre ci-dessus portent sur *quel* diagnostic une ligne fait taire. Celui-ci porte sur l'autre
moitié, et c'est celle que rien ne retrouve après coup : l'avertissement a disparu, il ne reste donc
rien à réexaminer, et la raison pour laquelle il était acceptable n'a vécu que dans la tête de qui a
écrit l'attribut.

**La présence est tout ce qu'il demande.** La valeur est lue pour sa longueur, jamais pour son sens —
une raison d'un mot le satisfait, et une que vous auriez mieux rédigée aussi. Juger ce que *dit* une
justification est une question humaine et le reste. L'unique valeur refusée est `"<Pending>"`, le
marqueur que Visual Studio écrit quand il génère une suppression pour vous : c'est le mot de l'outil
pour *pas encore rempli*.

Il tient **toute** suppression, y compris une suppression entièrement écrite en littéraux — un
littéral fait taire un avertissement exactement comme une référence. Aucun correctif n'est proposé,
parce que ce qui doit y figurer est la seule partie de l'attribut qu'un outil ne peut pas lire dans
votre code ([ADR-0039](../adr/0039-require-a-justification-on-every-suppression.fr.md)).

## Les transformer en erreurs de build

Chacun d'eux est une erreur par défaut, car aucun ne signale une ligne qui fait son travail : la
paire nomme deux règles, ou c'est un littéral qu'une référence remplacerait, ou c'est une paire à
moitié migrée, ou c'est une ligne que le *trimmer* jette, ou elle ne dit jamais pourquoi elle existe
([ADR-0040](../adr/0040-grade-every-dcat-diagnostic-by-what-it-says.fr.md)). Tous se configurent
comme n'importe quel diagnostic Roslyn :

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion   # migration progressive
dotnet_diagnostic.DCAT0014.severity = suggestion   # écrire les raisons jamais écrites
```

Cela a un coût qu'il vaut mieux connaître avant de référencer un catalogue. Sur une base de code
existante, `DCAT0006` se déclenche sur **toutes** les suppressions littérales d'un coup, et étant une
erreur il casse le build ce jour-là — `TreatWarningsAsErrors` n'y est plus pour rien. Descendez-le à
`suggestion`, migrez à votre rythme, puis supprimez la ligne.

`DCAT0014` arrive sur ce même premier build, et pose sa question à toutes vos suppressions plutôt
qu'aux seules qu'un catalogue sait apparier — sur une base de code qui n'a jamais écrit de
justifications, c'est donc le plus bruyant des deux. Il figure sur la même ligne d'abaissement
ci-dessus, et s'en retire de la même façon.

`DCAT0001`, `DCAT0007` et `DCAT0009` sont ceux qu'il faut laisser tranquilles. Aucun ne se déclenche
en masse : ils n'existent que là où quelqu'un a déjà commencé à utiliser des références, ou là où une
suppression de *trimmer* a été écrite à la main.

## Ce que ça laisse dans mon application

Rien. `SuppressMessageAttribute` est `[Conditional("CODE_ANALYSIS")]`, ce qui veut dire que le
compilateur ne l'écrit pas du tout dans votre assemblage à moins que vous ne le demandiez. Les
constantes sont repliées avant cela, si bien qu'il ne reste rien de toute la suppression : pas
d'attribut, pas de chaînes, pas de référence au catalogue.

Le paquet catalogue est une commodité de compilation. Ce n'est pas une dépendance d'exécution, et
c'est asserté par un test plutôt que promis — voir `tests/DiagnosticCatalog.ZeroFootprint.UnitTests`,
et [la garantie d'empreinte nulle](zero-footprint.fr.md) pour ce que ce test établit exactement, et
ce qu'il n'établit pas.

L'unique exception est délibérée : `UnconditionalSuppressMessage` ne porte pas de `[Conditional]`,
précisément pour que le *trimmer* puisse le lire dans l'assemblage compilé bien après que le
compilateur a fini. Il est préservé, avec les valeurs du catalogue repliées dedans comme de simples
chaînes.

## Deux choses que ceci ne peut pas aider

Dit franchement plutôt que laissé à votre découverte :

| Ce que vous écrivez | Pourquoi c'est hors de portée |
| --- | --- |
| `#pragma warning disable S1144` | Prend un identifiant nu, pas une expression. Aucune constante ne peut y être substituée, jamais. |
| `dotnet_diagnostic.S1144.severity` dans `.editorconfig` | Les clés de configuration sont du texte brut, entièrement hors du modèle de compilation C#. |

Et une frontière qui mérite d'être claire : ceci vérifie qu'une suppression est **structurellement
cohérente** — qu'elle nomme une vraie règle, de façon cohérente, et qu'elle dise pourquoi
([`DCAT0014`](diagnostics.fr.md#dcat0014)). Elle n'a aucune opinion sur le fait que supprimer cette
règle *à cet endroit* était une bonne idée, ni sur la raison que vous en donnez : la `Justification`
doit être écrite et n'est jamais pesée. Ce jugement reste le vôtre ; ce que les analyseurs refusent,
c'est la ligne qui n'a jamais été écrite.

## Où regarder ensuite

* [`DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self/README.fr.md) — les règles `DCAT`
  elles-mêmes en tant que catalogue, pour quand vous supprimez l'un de *ces* diagnostics.
* [La référence des diagnostics](diagnostics.fr.md) — chaque identifiant `DCAT`, ce qui le
  déclenche, comment le configurer.
* [Publier un catalogue](authoring-a-catalogue.fr.md) — si votre équipe possède un analyseur.

---

<div align="center">
<a href="./concepts.fr.md">← Concepts</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./configuration.fr.md">Configuration →</a>
</div>
