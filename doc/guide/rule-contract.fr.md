# Le contrat de règle

🌍 **Langues :**  
🇬🇧 [English](./rule-contract.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque a besoin de la forme exacte plutôt que de la plus courte — écrire un générateur,
relire un catalogue écrit à la main, ou comprendre pourquoi une déclaration n'est pas reconnue. La
source normative est [la spécification](../specification.fr.md), §7 à §10 ; ceci en est la distillation.

## Le contrat en entier, en cinq exigences

Une règle est un type qui satisfait les cinq :

| # | Exigence | Signalée par |
| --- | --- | --- |
| 1 | Marqué `[DiagnosticRule]` | — (un type non marqué n'est simplement pas une règle) |
| 2 | Une **classe statique, non générique** | `DCAT0002` |
| 3 | Une `const string` publique nommée `Id`, non vide | `DCAT0003` |
| 4 | Une `const string` publique nommée `Category`, non vide | `DCAT0004` |
| 5 | Que ce `Category` atteigne une constante déclarée dans une classe `[DiagnosticCategory]` | `DCAT0011` |

```csharp
[DiagnosticCategory]
internal static class JdCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = JdCategory.Usage;
}
```

Rien d'autre n'est exigé. Pas de classe de base, pas d'interface, rien à enregistrer.

Les exigences 4 et 5 sont deux questions sur le même membre, et il vaut la peine de les garder
distinctes. La quatrième demande si la catégorie peut être un argument d'attribut ; la cinquième
demande si la valeur a une déclaration ou plusieurs. La première parle d'une règle qui ne fonctionne
pas. La seconde parle d'un catalogue qui fonctionne et qui dérive.

## Pourquoi structurel plutôt qu'hérité

Une règle ne peut pas hériter de son contrat, et c'est un fait du langage plutôt qu'une préférence.

Une `const` ne peut pas être déclarée par une interface ni redéfinie depuis une classe de base, et une
propriété abstraite ne pourrait jamais être un argument d'attribut — ce qui est tout l'objet. Une
classe statique ne peut pas participer à l'héritage classique du tout.

Le contrat est donc **vérifié par un analyseur** plutôt qu'imposé par un système de types qui n'a
aucun moyen de l'imposer
([ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.fr.md)).

## Le marqueur est apparié par nom, jamais par symbole

`[DiagnosticRule]` est reconnu par son **nom de métadonnée pleinement qualifié** :

```text
DiagnosticCatalog.DiagnosticRuleAttribute
```

C'est une exigence de correction, pas une optimisation, et deux comportements en dépendent :

* **Un catalogue peut déclarer le marqueur lui-même** plutôt que prendre une dépendance de paquet —
  le motif que PolySharp emploie pour `IsExternalInit`. Son attribut est un symbole différent, et une
  comparaison de symboles ne l'apparierait jamais.
* **Un attribut non résoluble s'apparie quand même.** Quand la compilation d'un consommateur ne peut
  pas résoudre `DiagnosticCatalog.dll`, `[DiagnosticRule]` se dégrade en type d'erreur — qui conserve
  son nom. Une comparaison de symboles ne trouverait rien, ne signalerait rien, et produirait une
  sortie indiscernable d'une base de code sans problème : exactement la défaillance que cette
  bibliothèque existe pour éliminer, reproduite dans l'outil censé la détecter.

La réciproque tient aussi : un attribut du même nom **court** dans un autre espace de noms appartient
à quelqu'un d'autre et n'est délibérément pas apparié.

## `Id` — et quand il diffère du nom du type

La forme recommandée est `nameof`, qui ne peut pas diverger du type qu'elle nomme :

```csharp
public const string Id = nameof(JD0007);
```

Mais l'id est l'identifiant canonique du diagnostic, et **tout identifiant n'est pas un identifiant C#
valide**. Quand ils diffèrent, le nom du type cède :

```csharp
[DiagnosticRule]
public static class RULE_001
{
    public const string Id = "RULE-001";
    public const string Category = ContosoCategory.Usage;
}
```

Une valeur nulle, vide ou faite d'espaces compte comme **absente** — `DCAT0003`, et non une règle à
l'id vide.

## `Category` — le membre que rien ne peut vérifier

Même forme, mêmes règles, plus l'exigence 5 sur la provenance de la valeur. Ce qu'aucune exigence
n'atteint, c'est la valeur *elle-même*, et la distinction mérite d'être exacte : l'exigence 5 vérifie
que la catégorie a une déclaration unique, jamais que la chaîne qu'elle contient est la bonne. Ce
qu'elle devrait être, c'est la catégorie que déclare le `DiagnosticDescriptor` de l'analyseur
d'origine, et rien dans la plateforme ne compare les deux.

Ce n'est pas un manque de cette bibliothèque — c'est la propriété à cause de laquelle elle existe.
L'exactitude ici relève de la crédibilité du catalogue, ce qui explique que les catalogues de ce dépôt
soient générés depuis les descripteurs plutôt que transcrits
([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.fr.md)).

## Les catégories déclarées une seule fois — exigence 5

Une `const` initialisée depuis une autre `const` est **toujours une constante de compilation** :

```csharp
[DiagnosticCategory]
internal static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;   // toujours valide comme argument
}
```

`[DiagnosticCategory]` est **exigé**, et c'est l'exigence 5 qui l'exige. Les constantes se replieraient
à l'identique sans lui ; ce que le marqueur apporte, c'est que l'outillage peut distinguer une
constante de catégorie de n'importe quelle autre constante `string` de l'assemblage, ce qui est ce qui
permet à un correctif de proposer la constante nommée à la place d'un littéral. Dans un catalogue
généré par ce dépôt le conteneur est `internal`, si bien qu'une suppression ne nomme une catégorie
qu'à travers la règle qui la porte — voir
[ADR-0026](../adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md). Un catalogue
écrit à la main peut encore en publier un ; le contrat ne l'interdit pas, et le conteneur peut vivre
dans un autre assemblage.

La décision de l'exiger, et ce qu'elle n'achète délibérément pas, est
[ADR-0028](../adr/0028-require-every-rule-to-reach-its-category-through-a-declared-constant.fr.md).

## Quels attributs sont analysés

| Attribut | Analysé | Note |
| --- | --- | --- |
| `SuppressMessageAttribute` | oui | Le cas ordinaire. Non émis dans votre assemblage — il est `[Conditional("CODE_ANALYSIS")]`. |
| `UnconditionalSuppressMessageAttribute` | oui | Trim/AOT seulement. **Est** émis, et son décodeur n'accepte que les identifiants `IL####` — d'où `DCAT0009`. |

**Les alias sur l'attribut lui-même sont résolus.** L'analyse ne dépend jamais du nom court écrit dans
la source :

```csharp
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

[Suppress(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "...")]
```

## Formes syntaxiques acceptées sur un site d'utilisation

L'analyse travaille sur les **symboles Roslyn**, pas sur le texte source : toute forme qui résout vers
le même membre est donc équivalente.

**L'accès qualifié à un membre** — la forme canonique :

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

**Un alias de type** — pleinement équivalent, et recommandé quand le nom du conteneur est long :

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Unused.Category, Unused.Id)]
```

**`using static`** — reconnu, **pas recommandé** :

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Category, Id)]
```

Deux directives `using static` pour deux règles dans un même fichier rendent `Category` et `Id`
ambigus, ce qui est une erreur de compilation. La forme marche pour une règle par fichier et casse dès
qu'une seconde suppression est nécessaire. L'analyseur la résout ; la documentation ne la promeut pas.

**Une constante intermédiaire** — vérifiable, contrairement à ce qu'une première lecture suggère :

```csharp
private const string RuleId = SonarRule.S1144.Id;

[SuppressMessage(SonarRule.S1144.Category, RuleId)]
```

Quand un argument résout vers un champ constant dont le type déclarant n'est *pas* un type de règle,
l'analyseur compare sa **valeur** constante, exactement comme pour un littéral. Ce n'est pas la forme
canonique et aucun correctif n'est proposé — mais ce n'est pas non plus un angle mort.

## Comment un identifiant est apparié

Roslyn tronque l'identifiant d'une suppression au **premier deux-points** avant l'appariement, et
cette bibliothèque fait de même. C'est ce qui rend reconnaissable la forme que génère le *Supprimer →
Dans la source* de Visual Studio :

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
```

Le suffixe est un nom convivial et ne porte aucun sens pour la plateforme. Le correctif de `DCAT0006`
l'abandonne : il dupliquait le titre de la règle, que le catalogue porte en documentation XML.

`UnconditionalSuppressMessage` honore la même forme — `IL2026:FriendlyName` — ce qui explique que
`DCAT0009` reproduise le décodeur du *trimmer* plutôt qu'un motif plus strict. Signaler un identifiant
que le *trimmer* honore *effectivement* reviendrait à vous demander de changer quelque chose qui
fonctionne.

## Ce qui est hors du modèle

| | Pourquoi |
| --- | --- |
| `#pragma warning disable S1144` | Prend un jeton identifiant nu, pas une expression. Aucune position ne pourrait accueillir une constante. |
| `dotnet_diagnostic.S1144.severity` | Une clé `.editorconfig` est du texte brut, lu entièrement hors du modèle de compilation C#. |
| `Severity` en membre de règle | Une énumération peut être `const`, mais `DiagnosticSeverity` vit dans `Microsoft.CodeAnalysis.Common` — la déclarer impose Roslyn à tous les consommateurs du catalogue. |
| Titre ou message localisé | `LocalizableString` ne peut pas être une `const`. Le catalogue couvre l'axe identifiant et catégorie ; les fichiers de ressources restent le bon outil. |

## Où aller ensuite

* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — ce qui est signalé quand une déclaration ou un
  site d'utilisation rate le contrat.
* [**Dépannage**](troubleshooting.fr.md) — quand le contrat semble satisfait et que rien n'est
  signalé quand même.
* [**La spécification**](../specification.fr.md) — §7 à §10, normatif, avec le comportement de la
  plateforme sur lequel chaque exigence repose.

---

<div align="center">
<a href="./diagnostics.fr.md">← Les diagnostics DCAT</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./troubleshooting.fr.md">Dépannage →</a>
</div>
