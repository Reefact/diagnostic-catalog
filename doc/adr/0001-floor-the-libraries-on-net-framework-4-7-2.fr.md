# ADR-0001 | Plancher le support .NET Framework des bibliothèques à 4.7.2

🌍 **Langues :**  
🇬🇧 [English](./0001-floor-the-libraries-on-net-framework-4-7-2.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Les bibliothèques livrées ciblent `netstandard2.0` et `net10.0`. C'est
`netstandard2.0` qui les rend consommables depuis .NET Framework ; le minimum
.NET Framework formel de `netstandard2.0` est 4.6.1.

Sur les versions de .NET Framework antérieures à 4.7.2, le support de
`netstandard2.0` repose sur des façades rétro-ajoutées, des actifs de paquet
supplémentaires et des redirections de liaison côté consommateur. .NET Framework
4.7.2 est la première version qui fournit les façades concernées en standard.

`netstandard2.0` est un contrat de **compilation**. Il contraint la surface d'API
que le compilateur accepte ; il ne décrit pas le runtime qui charge l'assemblage.
.NET Framework et .NET moderne diffèrent sur la globalisation (NLS contre ICU),
sur la façade `netstandard.dll` qui redirige les références de types, et sur des
parties de la pile de réflexion. Une build qui cible `netstandard2.0` ne prouve
donc rien quant au comportement sur .NET Framework.

Un identifiant de règle dans un catalogue est un contrat : les consommateurs le
référencent symboliquement, et une résolution qui diffère silencieusement d'un
runtime à l'autre est un défaut que le consommateur ne peut pas voir venir.

Les assemblages .NET Framework s'exécutent sous Windows uniquement ; l'image du
runner Windows de la CI embarque un runtime .NET Framework, et les runners Linux
n'embarquent aucun Mono. Le targeting pack `net472` nécessaire à la compilation
peut être fourni par un paquet NuGet d'assemblages de référence, donc aucune
installation côté runner n'est requise.

Une promesse de compatibilité qui n'est jamais exécutée ne peut pas fournir une
frontière de support digne de confiance.

## Decision

Les bibliothèques `netstandard2.0` livrées supportent .NET Framework 4.7.2 et
ultérieur, et ce support est prouvé en exécutant leurs suites de tests sur le
runtime .NET Framework 4.7.2 plutôt qu'inféré depuis la cible de compilation.

## Rationale

4.7.2 est la version la plus basse sur laquelle les bibliothèques peuvent être
consommées sans la plomberie de compatibilité fragile qu'exigent les versions
antérieures. En dessous, l'expérience d'un consommateur dépend de redirections de
liaison que ce dépôt n'écrit pas et ne peut pas vérifier.

C'est aussi la version la plus basse que le dépôt peut réellement exercer.
Aligner le plancher documenté sur un runtime exécuté en continu transforme une
déclaration d'intention en contrat vérifiable — ce qui est tout l'objet, étant
donné que les différences qui comptent ici (globalisation, façade, réflexion)
sont exactement celles qu'une cible de compilation seule masque.

La décision choisit délibérément la frontière praticable et testable plutôt que
le minimum théorique de `netstandard2.0`. Supporter 4.6.1 reviendrait à promettre
un comportement sur un runtime que le dépôt n'a aucun moyen d'exécuter, pour des
consommateurs sur une plateforme qui n'a pas reçu de nouvelle version majeure
depuis des années.

Restreindre l'exécution du plancher à Windows n'est pas une limitation que la
décision impose mais une propriété de la plateforme ; la build ordinaire et la
boucle locale restent intactes parce que la build interne du plancher est
conditionnée et désactivée par défaut.

Le plancher contraint aussi ce qu'une bibliothèque livrée peut employer : un
marqueur de compilateur que la bibliothèque de base de .NET Framework ne livre
pas ne peut pas être utilisé dans du code livré, puisqu'un consommateur compilant
contre .NET Framework devrait le fournir lui-même. Le code de test peut le
polyfiller ; le code produit non.

## Alternatives Considered

### Conserver le minimum formel de `netstandard2.0`, 4.6.1

Envisagé parce que c'est la revendication la plus large que la cible de
compilation autorise formellement, et qu'elle ne coûte rien à écrire.

Rejeté parce que la revendication serait invérifiée : le dépôt ne peut pas
s'exécuter sur 4.6.1, et le support y dépend d'une plomberie côté consommateur
hors de son contrôle. Une frontière de support que personne ne teste est une
frontière qui cède chez le consommateur.

### Plancher à 4.6.2

Envisagé parce qu'elle est maintenue plus longtemps que 4.6.1 et reste
sensiblement plus large que 4.7.2.

Rejeté parce qu'elle porte les mêmes contraintes de façades et de redirections de
liaison que 4.6.1, et qu'elle est tout aussi inexécutable avec la pile de tests
supportée. Elle achèterait une revendication plus large, du même genre non testé.

### Ne cibler que .NET moderne et abandonner `netstandard2.0`

Envisagé parce que cela supprime entièrement le plancher, le job Windows et la
question du polyfill, et simplifie chaque projet livré.

Rejeté parce que cela retire les consommateurs .NET Framework de l'ensemble
adressable. Les catalogues de règles de diagnostic décrivent des analyseurs qui
tournent contre des bases de code de longue vie, c'est-à-dire précisément là où
.NET Framework vit encore ; les exclure réduirait la portée de la bibliothèque
pour une économie de maintenance que la build interne conditionnée maintient
déjà faible.

### Faire confiance à la cible de compilation et sauter le job d'exécution

Envisagé parce que `netstandard2.0` fait déjà échouer la build sur une API que la
plateforme n'a pas, ce qui attrape une vraie classe d'erreurs sans rien coûter.

Rejeté parce que les défaillances dont il est question ici ne sont pas des
défaillances de surface d'API. La comparaison sensible à la culture, la
résolution de types via façade et la réflexion se comportent différemment à
l'exécution, sur une build que le compilateur a acceptée.

## Consequences

### Positive

* La déclaration de support .NET Framework est exécutée à chaque pull request
  plutôt qu'affirmée.
* Les consommateurs sur .NET Framework évitent la fragilité des redirections de
  liaison des runtimes antérieurs à 4.7.2.
* La frontière est stable : .NET Framework ne reçoit plus de nouvelles versions
  majeures, le plancher a donc peu de chances de bouger.

### Negative

* Les consommateurs sur .NET Framework 4.6.1 à 4.7.1 sont hors de la plage
  supportée.
* Une branche CI Windows doit être maintenue en plus de la matrice ordinaire.
* Le code livré ne peut pas employer de fonctionnalités de langage dont les
  marqueurs de compilateur manquent à la bibliothèque de base de .NET Framework.

### Risks

* Un projet de test qui exerce une bibliothèque livrée oublie de rejoindre le
  plancher, si bien que la bibliothèque est compilée pour `netstandard2.0` mais
  jamais exécutée dessus. Atténuation : l'appartenance est déclarée par l'import
  du projet lui-même et le job CI découvre les importateurs plutôt que de lire
  une liste, si bien que rejoindre est une modification d'une ligne dans le
  projet concerné et ne peut pas être oublié à un second endroit.
* Le job de plancher est configuré mais pas bloquant, si bien qu'un plancher
  rouge n'empêche pas une fusion. Atténuation : ADR-0005 — le job doit être une
  vérification de statut requise.

## Follow-up Actions

* Maintenir la déclaration de support publique à .NET Framework 4.7.2 ou
  ultérieur.
* Faire du job framework-floor une vérification de statut requise quand la
  protection de branche sera configurée.

## References

* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md) —
  pourquoi une vérification configurée n'est pas encore une vérification
  bloquante.
* `build/Net472TestFloor.props` et `.github/workflows/ci.yml` — le mécanisme.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — « The .NET Framework floor ».
