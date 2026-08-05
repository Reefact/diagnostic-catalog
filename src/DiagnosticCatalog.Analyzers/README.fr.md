# DiagnosticCatalog.Analyzers

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Analyzers/README.en.md) | 🇫🇷 Français (ce fichier)

Analyseurs Roslyn pour [DiagnosticCatalog](https://github.com/Reefact/diagnostic-catalog).

Ils vérifient deux choses : qu'une **déclaration** de règle satisfait le contrat structurel — sa forme,
son `Id`, sa `Category`, la façon dont cette catégorie est atteinte et ce que dit le nom de son type — et
qu'une **suppression** qui en référence une est cohérente : deux arguments qui ne nomment pas la
`Category` d'une règle et l'`Id` de cette même règle, une suppression à moitié migrée mêlant une
référence et un littéral, un littéral qu'une référence de catalogue remplacerait, et un
`UnconditionalSuppressMessage` que le trimmer jette.

## Migrer une base de code existante

Le paquet porte aussi le correctif pour le dernier de ces cas, qui est la façon dont une base de code
adopte un catalogue en pratique :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
// devient
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

*Corriger toutes les occurrences* l'applique à un document, un projet ou une solution en une étape, et le
`using` dont la référence a besoin est ajouté pour vous. Tout le reste de l'attribut est laissé exactement
tel qu'écrit — `Justification`, `Scope`, `Target` et `MessageId` sont à vous.

Deux comportements à connaître avant de le lancer :

* **Le suffixe de nom convivial est retiré.** Visual Studio écrit
  `"S1144:Unused private members should be removed"` ; le correctif reconnaît cette forme et remplace le
  tout par la référence. La prose ne vivait dans la suppression que parce que rien d'autre ne la portait —
  la documentation propre à la règle s'en charge désormais.
* **Quand deux catalogues décrivent la même règle, aucun correctif n'est proposé.** Le diagnostic apparaît
  quand même, donc rien n'est caché, mais choisir entre les deux vous revient.

Une suppression laissée à moitié migrée — une référence, un littéral — est signalée elle aussi, et complétée
depuis la règle que l'argument déjà migré nomme :

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144", Justification = "kept for reflection")]
// devient
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

Seul le littéral est réécrit ; quelle que soit l'orthographe que vous avez choisie de l'autre côté, un alias
compris, elle est laissée intacte. Et si le littéral nomme quelque chose que la règle référencée ne nomme
pas — `"S9999"` à côté de `SonarRule.S1144.Category` — vous obtenez le diagnostic et aucun correctif.
Compléter celui-là ferait taire une autre règle que celle qui est tue aujourd'hui, et c'est une décision qui
vous revient, pas à une ampoule.

## Quand les deux arguments nomment des règles différentes

Ce cas-là reçoit **deux** correctifs et aucune recommandation :

```text
Use SonarRule.S1144.Id        — keep the category, correct the identifier
Use SonarRule.S2094.Category  — keep the identifier, correct the category
```

Vous seul savez laquelle des deux moitiés était la faute de frappe, donc aucune n'est proposée par défaut.
Bon à savoir pendant que vous choisissez : Roslyn apparie une suppression sur l'**identifiant seul** et ne
regarde jamais la catégorie, si bien que corriger la catégorie laisse exactement en l'état ce qui est
supprimé, tandis que corriger l'identifiant le change.

## Écrire une règle à la main

Un catalogue est normalement généré, et du code généré satisfait le contrat par construction. Quand vous en
écrivez un vous-même, des correctifs sont là pour la partie mécanique :

```csharp
[DiagnosticRule]
public sealed class JD0007                      // → Rendre 'JD0007' static
{
    private static readonly string Id = "JD0007";   // → Faire de 'Id' une constante publique
                                                    // → Déclarer 'public const string Category'
}
```

Chacun n'est proposé **que là où la réparation est déjà écrite dans le code**. `static` n'est pas proposé
pour un type générique, pour une `struct`, ni pour une classe portant un membre d'instance — le mot-clé n'y
compilerait pas, et retirer ce qui l'en empêche est un changement de votre conception plutôt qu'une
réparation de celle-ci. Une classe `partial` est refusée elle aussi : les parties que le correctif ne voit
pas sont celles qui décident.

Les réparations de membres corrigent des modificateurs et jamais la valeur. Un `const int Id`, une chaîne
vide, un initialiseur qui n'est pas constant — ceux-là sont signalés sans correctif, parce que le code ne dit
rien de ce que vous vouliez.

> **Celui auquel réfléchir avant d'appuyer.** *Déclarer 'public const string Category'* écrit
> `"TODO"`. C'est une vraie chaîne, donc `DCAT0004` cesse d'être signalé — vous avez échangé un
> avertissement contre un marqueur. Une catégorie que personne ne remplit est fausse pour toujours et
> invisible dans toutes les compilations, parce que Roslyn apparie une suppression sur son identifiant seul.
> `Id` est différent : il est écrit `nameof(JD0007)`, lu sur la déclaration plutôt qu'inventé.

## Le référencer

Les assemblys d'analyse ne doivent jamais devenir des dépendances d'exécution, donc référencez-le en privé :

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

Un paquet de catalogue qui référence celui-ci apporte les analyseurs à **ses** consommateurs aussi, si bien
que référencer le catalogue seul suffit dès lors que l'un d'eux le fait. Cela a été mesuré contre une
restauration réelle plutôt que lu dans la documentation de NuGet, qui dit le contraire :

| Un catalogue référençant ce paquet avec | Les analyseurs tournent chez ses consommateurs |
| --- | --- |
| aucun `PrivateAssets` | **oui** |
| `PrivateAssets="none"` | oui |
| `PrivateAssets="all"` | non |

Si vous publiez un catalogue et préférez ne pas imposer l'analyse à tout le monde en aval, dites-le
explicitement avec `PrivateAssets="all"` — c'est le silence qui se propage.

## Ce qu'ils ne font pas

Ils ne valident pas une chaîne arbitraire. `[SuppressMessage("Usage", "S1144")]` avec la mauvaise catégorie
ne correspond à aucune règle connue, et rien n'est signalé — le mécanisme qui rend une catégorie fausse
impossible est la constante elle-même, que le compilateur vérifie. Ces analyseurs vous amènent aux
constantes et vous y maintiennent.

## Documentation

- [**Les diagnostics `DCAT`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.fr.md)
  — chaque identifiant que ces analyseurs signalent, ce qui le déclenche, pourquoi il existe, si un
  correctif est proposé, et la clé `.editorconfig` qui le configure.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — les sévérités, le commutateur par catégorie, le code généré, et l'erreur de `PrivateAssets`
  qui fait tout taire.
- [**Adopter un catalogue sur une base de code existante**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.fr.md)
  — la montée en sévérité et dans quel ordre convertir, quand la migration ci-dessus est vaste.
- [**Le contrat de règle**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/rule-contract.fr.md)
  — les cinq exigences contre lesquelles ces analyseurs vérifient une déclaration, et chaque forme
  syntaxique qu'un site d'utilisation peut prendre.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme, à commencer par « rien n'est signalé du tout ».

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.

## Licence

Apache-2.0. Non officiel ; sans affiliation avec aucun éditeur d'analyseur.
