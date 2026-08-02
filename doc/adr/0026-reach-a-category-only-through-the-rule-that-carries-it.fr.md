# ADR-0026 | N'atteindre une catégorie qu'à travers la règle qui la porte

🌍 **Langues :**  
🇫🇷 Français (ce fichier) | 🇬🇧 [English](./0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)

**Statut :** Proposed
**Proposé :** 2026-08-02
**Décideurs :** Reefact

## Contexte

Un catalogue généré publie deux choses qu'une suppression peut nommer. Le membre
de règle :

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

Et, jusqu'à cette décision, la constante de catégorie seule :

```csharp
[SuppressMessage(SonarCategory.MajorCodeSmell, SonarRule.S1144.Id)]
```

Les deux compilent. Les deux se replient sur les deux mêmes chaînes. Aujourd'hui
elles sont indiscernables dans les métadonnées émises, et la seconde se lit
assez bien pour qu'une liste de complétion d'IDE y invite.

Elles cessent de concorder dès que le fournisseur déplace la règle. Quand
SonarSource recatégorise `S1144`, le catalogue est régénéré et
`SonarRule.S1144.Category` suit. `SonarCategory.MajorCodeSmell` non : elle nomme
toujours la catégorie où la règle *était*. La suppression continue de compiler,
continue de se lire comme délibérée, et cesse de correspondre au diagnostic pour
lequel elle a été écrite. C'est l'échec silencieux et permanent que cette
bibliothèque existe pour empêcher, réintroduit par un membre que la bibliothèque
a elle-même publié.

Les analyzers signalent la forme découplée, et c'est ainsi qu'elle a été
trouvée : la suite d'usage l'a écrite comme du code consommateur légitime et le
build a échoué. Mais la signaler est un mauvais remède, pour trois raisons. Le
message appelle la catégorie `the literal "Major Code Smell"`, alors qu'aucun
littéral n'est dans cette source. Le diagnostic qui tire, `DCAT0007`, est défini
comme *mélanger une référence de catalogue et un littéral*, ce qui n'est pas ce
qui s'est passé. Et un avertissement est un conseil : il peut être supprimé,
ignoré, ou jamais montré par un build qui ne les traite pas en erreurs.

## Décision

**Une constante de catégorie ne fait pas partie de la surface publique d'un
catalogue.** Le générateur émet le conteneur `[DiagnosticCategory]` en
`internal`.

Le membre `Category` de la règle elle-même reste public et ne change pas :

```csharp
public const string Category = SonarCategory.MajorCodeSmell;   // toujours public, toujours replié
```

Un `const` initialisé depuis un autre `const` reste une constante de compilation,
donc le membre public porte la valeur littérale et aucun consommateur ne perd
quoi que ce soit d'utilisable légitimement. Ce qu'il perd, c'est la possibilité
de nommer une catégorie *sans* nommer la règle à laquelle elle appartient — ce
qui est toute l'intention.

Le découplage devient ainsi **inécrivable** plutôt que déconseillé. Un
consommateur qui tente la mauvaise orthographe reçoit `CS0122` du compilateur au
point d'usage, pas un avertissement qu'il verra peut-être, et pas une règle de
lint qu'il peut désactiver.

## Conséquences

**Ceci supersede une partie de l'[ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.fr.md).**
Le contexte de ce record énonce qu'un catalogue publie « une constante de
catégorie, référencée comme `SonarCategory.MajorCodeSmell` » dans « la source du
consommateur, à l'intérieur d'arguments de `SuppressMessageAttribute` ». Après
cette décision, cette phrase décrit quelque chose qui ne compile plus. La règle
effective de l'ADR-0012 — un catalogue ne renomme jamais un membre qu'il a
publié — est intacte et lie toujours chaque membre public. Seule sa prémisse sur
l'ensemble des membres publics se rétrécit. L'ADR-0012 n'est pas édité ici ; si
ceci est accepté, l'enregistrement de son statut de superseded revient au
mainteneur.

**C'est un changement cassant sur trois paquets publiés.**
`DiagnosticCatalog.Sonar`, `DiagnosticCatalog.NetAnalyzers` et
`DiagnosticCatalog.StyleCop` sont sur nuget.org en `0.2.1`, `0.2.1` et `0.3.0`.
Tout consommateur ayant écrit `SonarCategory.MajorCodeSmell` cesse de compiler à
la montée de version, avec une erreur de compilation claire et une réparation
d'une ligne : nommer la règle à la place. Le changement atterrit dans
`1.0.0-preview.1`, le moment le moins coûteux qu'il aura jamais — une preview
existe précisément pour qu'une décision de cette nature reste possible.

**Le récit de la spécification sur ce que le marqueur apporte se rétrécit.** Le
§7.7 dit que le marqueur `[DiagnosticCategory]` permet au correctif `DCAT0006`
de proposer `SonarCategory.MajorCodeSmell` plutôt qu'un littéral nu. Aucun
correctif ne l'implémente aujourd'hui, et après cette décision aucun ne le
devrait : proposer un membre interne à un consommateur ne compilerait pas. Le
marqueur conserve son autre objectif déclaré, permettre à l'outillage de
reconnaître une constante de catégorie, et en gagne un plus simple — il est ce
que le générateur marque pour qu'un futur contrôle puisse valider le contenu du
conteneur.

**Ceci ne corrige pas la constante intermédiaire.** Un consommateur qui écrit
`const string RuleId = SonarRule.S1144.Id;` et l'associe à la catégorie de cette
même règle reste signalé par `DCAT0007`, et le guide liste toujours cette forme
parmi les formes *acceptées*. C'est un défaut distinct dans
`SuppressionAttribute.Resolve`, qui classe par type déclarant et ne suit jamais
un initialiseur, et il n'est pas traité ici.

**Rien n'impose ceci au-delà du générateur.** Un catalogue écrit à la main peut
toujours publier un conteneur de catégories public ; le contrat ne l'interdit
pas, et `DCAT0002`–`DCAT0004` ne disent rien des catégories. Cette décision lie
ce que *ce dépôt génère*.

## Actions de suivi

* Signaler séparément le faux positif sur la constante intermédiaire, et
  trancher si `Resolve` doit suivre un saut — la trouvaille que cette décision
  ne couvre pas.
* Corriger le message de `DCAT0007`, qui nomme un littéral qui n'a pas besoin
  d'exister dans la source.
* Reformuler le §7.7 de la spécification pour que le bénéfice annoncé du
  marqueur ne nomme plus un correctif qui ne peut pas être proposé.
