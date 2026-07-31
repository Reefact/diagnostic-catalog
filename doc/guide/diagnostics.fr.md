# Les diagnostics `DCAT`

🌍 **Langues :**  
🇬🇧 [English](./diagnostics.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque a vu passer un `DCATxxxx` et veut savoir ce qu'il signifie. Chaque diagnostic que
`DiagnosticCatalog.Analyzers` signale : ce qui le déclenche, pourquoi il existe, comment le
configurer.

Ils se répartissent en deux groupes. Les diagnostics de **déclaration** regardent une règle que vous
avez déclarée ; vous ne les voyez que si vous écrivez un catalogue. Les diagnostics de **site
d'utilisation** regardent une suppression que vous avez écrite, ce qui concerne la plupart des gens.

| Identifiant | Regarde | Titre | Défaut | Correctif |
| --- | --- | --- | --- | --- |
| [`DCAT0001`](#dcat0001) | site d'utilisation | `Category` et `Id` doivent référencer la même règle | Avertissement | deux, non classés |
| [`DCAT0002`](#dcat0002) | déclaration | Une règle doit être déclarée comme classe statique non générique | Avertissement | — |
| [`DCAT0003`](#dcat0003) | déclaration | Une règle doit exposer une constante `string` publique nommée `Id` | Avertissement | — |
| [`DCAT0004`](#dcat0004) | déclaration | Une règle doit exposer une constante `string` publique nommée `Category` | Avertissement | — |
| [`DCAT0006`](#dcat0006) | site d'utilisation | Utiliser une référence de catalogue plutôt que des littéraux | Avertissement | oui |
| [`DCAT0007`](#dcat0007) | site d'utilisation | La suppression mêle une référence de catalogue et un littéral | Avertissement | oui, sous condition |
| [`DCAT0009`](#dcat0009) | site d'utilisation | `UnconditionalSuppressMessage` n'accepte que les identifiants `IL####` | Avertissement | — |

`DCAT0005`, `DCAT0008` et `DCAT0010` sont spécifiés mais délibérément hors de la 1.0.

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
> où vous ajoutez un catalogue. Sous `TreatWarningsAsErrors`, cela casse le build immédiatement.
> Descendez-le à `suggestion`, migrez avec *Corriger toutes les occurrences*, puis remontez-le.

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

---

## Diagnostics de déclaration

Ceux-ci se déclenchent sur du code qui déclare des règles. Voir
[le guide de l'auteur de catalogue](authoring-a-catalogue.fr.md).

### `DCAT0002`

**Marqué `[DiagnosticRule]` mais pas une classe statique non générique.** Une règle porte des
constantes et n'est jamais instanciée ; une règle générique n'a aucun membre constant à offrir.

### `DCAT0003`

**Pas de `const string Id` publique.** La cause habituelle est `static readonly` au lieu de `const` :
il a une valeur à l'exécution mais ne peut pas être un argument d'attribut, ce qui est tout l'objet.
Une valeur vide ou faite d'espaces compte comme absente.

Utilisez `nameof(LeTypeDeLaRègle)`, qui ne peut pas diverger du type qu'il nomme.

### `DCAT0004`

**Pas de `const string Category` publique.** Mêmes règles que pour `Id`.

Sa *valeur* devrait être celle que déclare le `DiagnosticDescriptor` de l'analyseur d'origine. Rien
dans la plateforme ne le vérifie — ce qui est exactement pourquoi la constante vaut la peine.

---

## Les configurer

Mécanismes Roslyn standards, aucun format propriétaire :

```ini
# .editorconfig
[*.cs]

# Une suppression qui nomme deux règles ne fait pas ce qu'elle a l'air de faire.
dotnet_diagnostic.DCAT0001.severity = error
dotnet_diagnostic.DCAT0007.severity = error

# Une suppression que le trimmer jette.
dotnet_diagnostic.DCAT0009.severity = error

# Migration progressive : visible dans l'IDE, hors du build.
dotnet_diagnostic.DCAT0006.severity = suggestion

# Déclarer des règles — vous n'en avez besoin que si vous publiez un catalogue.
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
```

La catégorie est `DiagnosticCatalog`, vous pouvez donc aussi les régler tous d'un coup :

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Cantonnez une section à un chemin de la façon ordinaire d'`.editorconfig` quand du code généré ou un
dossier hérité demande un traitement différent.

## Ce qui n'est délibérément pas vérifié

Les analyseurs vérifient qu'une suppression est **structurellement cohérente** — qu'elle nomme une
vraie règle, de façon cohérente. Ils ne font pas, et ne feront pas :

* valider une chaîne arbitraire. `[SuppressMessage("Usage", "S1144")]` avec une mauvaise catégorie ne
  correspond à aucune règle connue et n'est signalé par rien. Ce qui rend une mauvaise catégorie
  impossible, c'est la *constante*, que le compilateur vérifie — ces diagnostics vous amènent aux
  constantes et vous y maintiennent ;
* juger si supprimer une règle *à cet endroit* était raisonnable. C'est à cela que sert
  `Justification`, et cela reste une question humaine ;
* atteindre `#pragma warning disable` ou les clés de gravité d'`.editorconfig`, qui prennent du texte
  nu hors du modèle de compilation C#. Aucune constante ne peut jamais y être substituée.

---

<div align="center">
<a href="./authoring-a-catalogue.fr.md">← Publier un catalogue</a> · <a href="./README.fr.md">↑ Table des matières</a>
</div>
