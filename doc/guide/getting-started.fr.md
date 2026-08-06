# Démarrer

🌍 **Langues :**  
🇬🇧 [English](./getting-started.en.md) | 🇫🇷 Français (ce fichier)

<!-- dcat-doc:missing SonarRule.S1145 l'erreur volontaire de l'étape 3 ; toute l'étape est le CS0117 qu'elle produit -->

Pour quiconque a un projet qui fait déjà taire des avertissements d'analyseur. Dix minutes, une
référence de paquet, et une erreur volontaire — l'erreur est le cœur de l'affaire.

Vous allez :

1. référencer un catalogue ;
2. réécrire une suppression contre des constantes ;
3. la casser exprès et regarder le compilateur l'attraper ;
4. décider de ce que fait le reste de la base de code.

## Ce qu'il vous faut

Un projet C# qui exécute déjà un analyseur et fait taire au moins un de ses avertissements — Sonar,
les règles .NET `CAxxxx`, ou StyleCop. Si vous n'avez aucune suppression, il n'y a encore rien ici
pour vous ; [quand ne pas s'en servir](when-not-to-use.fr.md) le dit franchement.

Rien d'autre. Pas de version de SDK à monter, pas d'outil à installer, pas de générateur à lancer.

## 1. Référencer un catalogue

Prenez celui qui correspond à l'analyseur dont vous faites taire les avertissements :

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

**[L'index des catalogues](../README.fr.md#-les-catalogues-disponibles)** est la liste : une ligne
par analyseur, avec le préfixe de règles qu'il couvre. Référencez-en plusieurs si vous exécutez
plusieurs analyseurs. La liste n'est délibérément pas recopiée ici — une copie partielle est la façon
dont un lecteur conclut que son analyseur n'est pas couvert.

Cette unique référence active aussi les vérifications : chaque catalogue dépend de
`DiagnosticCatalog`, qui porte les analyseurs `DCAT` et leurs correctifs à côté des attributs
marqueurs, et qu'aucun catalogue n'a le droit de masquer
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)). Il n'y a pas de
second paquet à ajouter — et si vous voulez les vérifications sans aucun catalogue,
`DiagnosticCatalog` seul est la réponse.

Deux conséquences tombent dès la toute première compilation, toutes deux en **erreur**. `DCAT0006`
signale chaque suppression littérale qui correspond à une règle que vous avez désormais : une base de
code qui en a déjà quelques centaines les rencontre toutes d'un coup. `DCAT0014` signale chaque
suppression qui n'a jamais dit pourquoi elle existe — toutes, règle appariée ou non.
Descendez les deux d'abord, et la visite reste une visite :

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion
dotnet_diagnostic.DCAT0014.severity = suggestion
```

[Adopter un catalogue](adopting-a-catalogue.fr.md) est ce dont cette ligne est la première étape ;
l'étape 4 y revient.

## 2. Réécrire une suppression

Trouvez une suppression que vous avez déjà. Elle ressemble à ceci :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Remplacez les deux chaînes par les deux constantes :

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Compilez. Ça compile, l'avertissement est toujours tu, et l'assemblage que vous obtenez est octet
pour octet celui que vous aviez avant — voir [l'étape 5](#5-mesurer-ce-que-ça-vous-a-coûté).

Tapez `SonarRule.` et IntelliSense liste chaque règle du catalogue ; tapez `S1144` et la liste s'y
réduit. Survolez la constante et vous obtenez le titre de la règle — c'est là que vit désormais la
prose que vous colliez autrefois dans la suppression.

## 3. La casser exprès

C'est l'étape qui vaut d'être faite plutôt que lue. Changez un chiffre, et laissez la justification
exactement où elle était — la seule chose à l'épreuve ici est l'identifiant :

```csharp
[SuppressMessage(
    SonarRule.S1145.Category,
    SonarRule.S1145.Id,
    Justification = "Appelé par le sérialiseur.")]
```

Compilez à nouveau :

```text
error CS0117: 'SonarRule' does not contain a definition for 'S1145'
```

Faites maintenant la même chose sur la version de départ :

```csharp
[SuppressMessage("Major Code Smell", "S1145", Justification = "Appelé par le sérialiseur.")]
```

Compilez. **Ça compile.** Aucun compilateur, analyseur, test ni outil de la plateforme ne juge cette
paire : rien ne vous dit donc que l'identifiant est faux — et l'avertissement que la suppression
masquait est silencieusement de retour, ou silencieusement pas, selon que le code qui le levait est
encore là.

Soyez précis sur ce que « rien ne le signale » veut dire, car c'est plus étroit qu'il n'y paraît :

* **La paire n'est jugée par rien.** Roslyn fait la correspondance sur le seul identifiant et ne lit
  jamais la catégorie, et `DCAT0001` compare deux membres d'une règle que votre compilation voit —
  deux chaînes ne lui donnent rien à comparer. C'est le trou, et il est total.
* **`DCAT0014` est une autre question.** Si vous aviez aussi retiré la `Justification`, cette ligne
  aurait été signalée — pour n'avoir aucune raison, pas pour nommer une règle inexistante. Garder la
  justification dans les deux versions ci-dessus est ce qui laisse l'identifiant seul sous la lampe.

Cette différence, c'est toute la bibliothèque. [Pourquoi les chaînes magiques
échouent](the-problem.fr.md) démonte la seconde compilation et explique pourquoi rien, dans la
plateforme, n'est en mesure de le signaler.

Remettez le chiffre.

## 4. Décider de ce que fait le reste de la base de code

Vous avez maintenant une suppression vérifiée et, très probablement, quelques centaines qui sont
encore des chaînes. Trois options honnêtes :

* **Les laisser.** Un catalogue est utile une suppression à la fois. Rien ne se dégrade parce que le
  reste du fichier est encore en littéraux — mais la ligne de gravité de l'étape 1 reste, puisque
  `DCAT0006` les signale toutes.
* **Convertir au fil de l'eau.** Réécrivez une suppression quand vous éditez déjà son fichier. Cela
  ne coûte rien de plus et touche le code qui bouge.
* **Convertir en masse.** C'est à cela que servent les analyseurs `DCAT` — ils signalent chaque
  suppression littérale qui correspond à une règle que vous avez, avec un correctif qui la réécrit et
  ajoute le `using`, et **Corriger toutes les occurrences** l'applique à un projet ou une solution en
  une étape.

  Ils sont déjà dans votre compilation : le catalogue les a amenés avec lui, et il n'y a rien
  d'autre à référencer.

Laquelle choisir est le sujet de la section adoption d'[Écrire des suppressions que le compilateur
vérifie](writing-suppressions.fr.md).

## 5. Mesurer ce que ça vous a coûté

Rien, et c'est mesurable plutôt qu'affirmé.

`SuppressMessageAttribute` est `[Conditional("CODE_ANALYSIS")]`. À moins que vous ne définissiez ce
symbole — et presque personne ne le fait — le compilateur n'écrit pas du tout l'attribut dans votre
assemblage. Les constantes sont repliées vers leurs valeurs avant ce point, si bien qu'il survit de
toute la suppression : rien. Pas d'attribut, pas de chaînes, pas de référence au catalogue, aucun
assemblage à charger au démarrage.

Le catalogue est une commodité de compilation, et le dépôt l'asserte par un test plutôt que de le
promettre — `tests/DiagnosticCatalog.ZeroFootprint.UnitTests`.

L'unique exception délibérée est `UnconditionalSuppressMessage`, qui ne porte pas de `[Conditional]`
précisément pour que le *trimmer* puisse le lire dans l'assemblage compilé. Là, les valeurs y sont
repliées comme de simples chaînes, ce que le *trimmer* voulait de toute façon.

## Ce que vous n'avez pas eu à faire

Cela vaut d'être nommé, parce que la plupart des outillages réclament tout cela :

* aucun générateur de source à lancer, et rien dans `obj/` qui doive rester en phase ;
* aucun fichier de configuration, sauf si vous voulez changer la gravité d'un diagnostic ;
* aucune dépendance d'exécution, et rien à initialiser au démarrage ;
* aucun changement à la façon dont vous compilez, testez ou publiez.

## Où aller ensuite

* [**Pourquoi les chaînes magiques échouent**](the-problem.fr.md) — ce que l'étape 3 a réellement
  démontré, et pourquoi l'argument de catégorie est la pire des deux moitiés.
* [**Concepts**](concepts.fr.md) — règle, catalogue, conteneur, catégorie : les quatre mots que le
  reste de la documentation emploie.
* [**Écrire des suppressions que le compilateur vérifie**](writing-suppressions.fr.md) — les alias,
  l'adoption sur une grosse base de code, et les deux choses que ceci ne peut pas atteindre.

---

<div align="center">
<a href="./the-problem.fr.md">← Pourquoi les chaînes magiques échouent</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./concepts.fr.md">Concepts →</a>
</div>
