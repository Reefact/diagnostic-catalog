# ADR-0031 | Trouver les analyzers comme le compilateur les trouve

🌍 **Langues :**  
🇬🇧 [English](./0031-find-analyzers-the-way-the-compiler-finds-them.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

Le générateur lit un package d'analyzer amont en chargeant ses assemblies et en
construisant les analyzers qu'elles déclarent, parce qu'un `DiagnosticDescriptor`
n'existe qu'à l'exécution (ADR-0009). Jusqu'ici il sélectionnait ces analyzers en
demandant à chaque assembly tous les types qu'elle déclare, et en gardant les
sous-classes non abstraites de `DiagnosticAnalyzer`.

Matérialiser un type résout son type de base et ses interfaces. Un package
d'analyzer n'est pas majoritairement fait d'analyzers : il porte des correctifs
de code, des helpers internes et des types de service, et ceux-là atteignent des
assemblies que le générateur n'a aucune raison de détenir. Quand l'une d'elles
ne se résout pas, l'énumération entière répond par une unique
`ReflectionTypeLoadException` portant les types qui ont survécu et aucun nom pour
les autres.

Une lecture qui a perdu une règle doit être refusée, car une règle absente est
indiscernable d'une règle retirée et serait publiée en `[Obsolete]`, disant aux
utilisateurs d'un éditeur quelque chose de faux sur le produit de cet éditeur
(ADR-0024, ADR-0010). Sans nom pour ce qui a été perdu, le générateur ne pouvait
pas distinguer un correctif de code d'un analyzer, et refusait donc le run.

Mesuré sur vingt packages d'analyzer, quatre étaient refusés ainsi :
`Roslynator.Analyzers`, `Roslynator.Formatting.Analyzers`,
`Microsoft.CodeAnalysis.Analyzers` et
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. Dans chaque cas les descriptors
avaient déjà été lus en entier ; ce qui n'avait pas chargé ne déclarait aucune
règle. Deux causes distinctes étaient en jeu — un service interne impossible à
construire, et des types internes implémentant une interface Roslyn qui a depuis
gagné un membre — et aucune n'est adressable par quoi que ce soit que le
générateur puisse embarquer.

Le compilateur, lui, n'énumère pas les types. Roslyn découvre les analyzers en
lisant les métadonnées d'une assembly pour y trouver les types marqués
`[DiagnosticAnalyzer]`, et ne charge que ceux-là. Un analyzer que l'attribut ne
nomme pas n'est chargé par aucun hôte et ne signale rien dans aucun build.

Deux descriptors ne sont atteignables que par des types que l'attribut ne nomme
pas. `SecurityCodeScan.VS2019` en déclare un dont l'identifiant est `Debug` et
dont le titre ne se résout pas, et `Microsoft.CodeAnalysis.CSharp.CodeStyle`
déclare `IDE0079`, dont l'analyzer est piloté par l'IDE via une interface
séparée plutôt que par la découverte d'analyzers ; avec `IDE0079` configuré en
avertissement et l'application du style activée, un build ne le signale pas du
tout sur une suppression inutile, là où le même montage signale `IDE0005`.

## Decision

Le générateur sélectionne les analyzers qu'une assembly marque
`[DiagnosticAnalyzer]`, lus depuis les métadonnées avant tout chargement, et
n'énumère plus les types qu'une assembly déclare.

## Rationale

Le refus avait raison au vu des éléments dont il disposait et tort sur les
packages qu'il écartait, et les deux tiennent au même fait : un type qui n'a pas
chargé n'a plus de nom auquel se référer. Rien ne pouvait être ajouté au
générateur pour rendre ce jugement fiable, parce que l'information nécessaire est
détruite par la défaillance même qu'il juge. La seule façon de répondre à « un
analyzer a-t-il disparu ? » est de savoir lesquels existent *avant* de charger
quoi que ce soit, ce que la lecture de l'attribut dans les métadonnées fournit.
Le refus est donc préservé et rendu précis plutôt qu'assoupli : un analyzer
attribué qui ne charge pas arrête toujours le run.

Suivre la découverte propre au compilateur tranche aussi la question de ce à quoi
un catalogue *sert*. Un catalogue existe pour que les arguments d'une suppression
soient vérifiés à la compilation, et une suppression n'a de sens que pour un
diagnostic que le build d'un consommateur peut signaler. La sélection par type de
base publiait des règles issues de types qu'aucun hôte ne charge — un descriptor
dont le titre ne se résout pas, et un autre appartenant à un analyzer que le
compilateur n'exécute jamais. Ces entrées invitaient un consommateur à référencer
une règle qui ne sera jamais levée là où la référence est vérifiée.

L'alternative consistant à lire tous les types et à ne refuser que lorsqu'un type
attribué est perdu a été rejetée sur le déterminisme, qui compte ici plus que la
couverture. Un catalogue est un fichier généré commité dans un dépôt et régénéré
périodiquement ; son contenu doit dépendre de l'assembly amont et de rien
d'autre. Avec cette hybridation, la présence d'une règle dépendrait de la
résolution d'un helper sans rapport sur la machine qui génère — donc la même
version amont produirait des catalogues différents sur le poste d'un mainteneur
et sur le runner nocturne, et l'écart se lirait comme un éditeur ajoutant ou
retirant des règles. Les métadonnées ne peuvent pas échouer ainsi : ce sont les
mêmes octets partout.

Le coût est assumé plutôt qu'écarté. `IDE0079` est une règle réelle et
documentée qu'un consommateur peut vouloir supprimer dans un éditeur, et elle
quitte l'ensemble atteignable. Elle n'est publiée aujourd'hui par aucun catalogue
livré par ce dépôt, donc rien en circulation ne change ; ce qui change, c'est ce
que contiendrait un futur catalogue des règles IDE. Le jugement est qu'un
catalogue décrivant uniquement ce qu'un build peut signaler vaut mieux qu'un
catalogue exhaustif sur des règles dont il ne peut pas rendre les références
utiles — et qu'une règle dont l'analyzer n'est chargé par aucun hôte vaut mieux
absente que présente et inapplicable.

## Alternatives Considered

### Continuer d'énumérer les types, et ne refuser que si un analyzer attribué est perdu

Considérée parce qu'elle est strictement plus inclusive : elle corrige les refus
abusifs tout en conservant chaque descriptor que le comportement précédent
produisait, `IDE0079` compris, et elle exige de lire l'attribut de toute façon.

Rejetée parce qu'elle fait dépendre le contenu d'un catalogue de l'environnement
qui l'a généré. Un type helper se résout ou non selon les assemblies joignables,
et les règles qui en dépendent apparaîtraient et disparaîtraient avec lui — vu en
aval comme un éditeur ayant changé son jeu de règles. Un artefact généré qui
n'est pas reproductible depuis son entrée est un défaut plus grave qu'une règle
manquante, parce que rien nulle part ne le signalerait.

### Fournir au cas par cas ce dont les types défaillants ont besoin

Considérée parce que cela a déjà fonctionné une fois : déployer
`Microsoft.Bcl.AsyncInterfaces` auprès du lecteur a débloqué trois packages, et
le même geste pourrait en débloquer d'autres.

Rejetée parce qu'elle ne termine pas et ne généralise pas. Les deux causes
mesurées ici ne sont pas des dépendances manquantes — un service interne qui
lève à la construction, et des types compilés contre un Roslyn plus ancien dont
l'interface a depuis gagné un membre — et aucune dépendance embarquée par le
lecteur n'y répond. Elle concède aussi la prémisse, à savoir que le lecteur
devrait charger ces types.

### Filtrer l'attribut selon le langage qu'il déclare

Considérée parce que `[DiagnosticAnalyzer]` nomme les langages qu'un analyzer
sert, et que les catalogues sont générés pour C# uniquement (ADR-0020).

Rejetée parce que le langage est déjà décidé en amont, par les assemblies que
l'acquisition sélectionne dans un package, et qu'un second filtre ne pourrait que
soustraire. Les packages livrant une seule assembly pour les deux langages sont
courants, et un analyzer dont les langages déclarés ne correspondraient pas à
l'orthographe attendue perdrait ses règles silencieusement — exactement la
défaillance que tout ce domaine existe pour empêcher.

## Consequences

### Positive

* Les quatre packages refusés pour des types ne déclarant aucune règle sont lus
  intégralement : `Roslynator.Analyzers` (242 règles),
  `Microsoft.CodeAnalysis.Analyzers` (52),
  `Roslynator.Formatting.Analyzers` (55) et
  `Microsoft.CodeAnalysis.BannedApiAnalyzers` (3).
* Les quatre catalogues livrés par ce dépôt se régénèrent octet pour octet à
  l'identique.
* Un manque est désormais un analyzer nommé plutôt qu'un décompte anonyme : un
  refus dit quelle règle est en jeu.
* Les assemblies ne déclarant aucun analyzer — l'essentiel d'un package
  d'analyzer — ne sont plus chargées du tout.

### Negative

* `IDE0079` et l'entrée `Debug` de `SecurityCodeScan.VS2019` quittent l'ensemble
  atteignable. Aucune n'est publiée aujourd'hui par un catalogue livré ici.
* Un analyzer ne déclarant pas l'attribut n'est plus catalogué même là où il
  charge proprement, ce qui est un changement de comportement pour tout package
  de ce genre non mesuré ici.

### Risks

* L'attribut est reconnu à son nom simple : un attribut homonyme sans rapport
  sélectionnerait donc un type. Le type de base est vérifié après chargement,
  donc le coût est un type ignoré, jamais une règle inventée.
* Un éditeur pourrait en principe compter sur un hôte autre que le compilateur
  pour exécuter un analyzer non attribué, comme Roslyn le fait lui-même pour
  `IDE0079`. Les règles atteintes ainsi sortent de ce qu'un catalogue peut
  promettre.

## Follow-up Actions

* Décider si un catalogue des règles IDE vaut d'être publié, sachant qu'`IDE0079`
  en serait absente.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — le contenu vient des descriptors
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) — pourquoi une règle absente est dangereuse
* [ADR-0020](0020-a-catalogue-is-generated-for-c-sharp-only.fr.md) — sélection du langage
* [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.fr.md) — refuser ce qui ne peut pas être vu
