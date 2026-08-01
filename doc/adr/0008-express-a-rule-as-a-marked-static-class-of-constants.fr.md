# ADR-0008 | Exprimer une règle en classe statique de constantes marquée, jamais en interface

🌍 **Langues :**  
🇬🇧 [English](./0008-express-a-rule-as-a-marked-static-class-of-constants.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

La bibliothèque existe pour remplacer les chaînes magiques passées à
`SuppressMessageAttribute` par des références qu'un compilateur peut vérifier.
Cet attribut expose un unique constructeur prenant une catégorie et un `checkId`,
tous deux positionnels et obligatoires ; une référence de règle n'est donc jamais
utile que dans une seule position : en argument d'attribut.

C# exige que chaque argument d'attribut soit déterminable à la compilation. Une
`const` convient. Une propriété, un champ `static readonly`, un `record` et une
instance statique non, et une constante ne peut être ni virtuelle ni redéfinie.
Aucune interface et aucune classe de base abstraite ne peut donc fournir les
valeurs dont un site d'utilisation a besoin : une implémentation pourrait
satisfaire un tel contrat et rester inutilisable au seul endroit où ses valeurs
sont lues. Une classe statique, qui est ce qu'un pur conteneur de constantes veut
être, ne peut participer à aucun héritage.

Une classe statique exposant deux constantes de chaîne est une forme ordinaire à
laquelle du code ordinaire arrive pour des raisons sans rapport. Rien dans la
forme seule n'énonce que son auteur entendait déclarer une règle de diagnostic.

Les catalogues sont livrés en paquets NuGet, et l'outillage découvre les règles
qu'un paquet référencé déclare en les lisant dans les métadonnées compilées de
cet assemblage, pas dans les sources. Un attribut marqué `[Conditional]` n'est
pas émis dans les métadonnées à moins que le symbole ne soit défini à la
compilation de l'assemblage *déclarant* ; `SuppressMessageAttribute` est
lui-même `[Conditional("CODE_ANALYSIS")]`, et c'est pourquoi la BCL a dû ajouter
un second attribut, non conditionnel, pour les consommateurs qui lisent les
suppressions dans les métadonnées.

Les arguments d'un attribut ne peuvent pas être référencés depuis un autre
attribut.

Au moment de cette décision, l'attribut marqueur est livré et l'analyseur qui
valide les déclarations contre lui ne l'est pas.

## Decision

Une règle de diagnostic est exprimée en classe statique non générique exposant
son identifiant et sa catégorie en constantes de chaîne publiques, marquée par un
attribut dédié, et ce contrat structurel est vérifié par un analyseur plutôt que
par le système de types.

## Rationale

L'expression objet de ce contrat n'est pas rejetée par goût ; le langage la
ferme. Quoi qu'imposent une interface ou une classe de base, ce serait imposé sur
des membres qui ne peuvent pas apparaître là où les valeurs doivent apparaître ;
le contrat serait donc satisfait par des types qui ne fonctionnent pas. Un
contrat qu'on peut honorer partout sauf au point d'utilisation est pire que pas
de contrat : il serait vérifié par le compilateur et laisserait quand même passer
la défaillance.

Une fois le système de types hors course, la question n'est pas d'accepter ou non
un contrat non vérifié, mais de choisir le vérificateur. Un analyseur est la
réponse naturelle, et pas seulement une réponse disponible : il peut asserter
exactement les propriétés qui comptent — que le type est statique et non
générique, qu'il porte un `Id` et une `Category`, qu'aucun des deux n'est vide —
et il les signale là où la déclaration est écrite, ce qu'un système de types
aurait fait. C'est en outre la même classe d'outil que la bibliothèque est faite
pour servir ; rien de nouveau n'est demandé à la build d'un consommateur.

L'attribut marqueur gagne sa place parce qu'il est la seule chose qui distingue
une règle de n'importe quelle autre classe statique de constantes. Sans signal
déclaré, l'outillage devrait inférer l'intention depuis la forme et signalerait
des types dont l'auteur n'a jamais opté pour ce mécanisme ; tout l'objet du
catalogue est qu'une référence de règle soit un contrat délibéré, et un contrat
que personne n'a choisi est une supposition. Déclarer l'intention coûte une ligne
là où la règle est écrite et rien du tout là où elle est utilisée, puisque la
référence se replie en littéral et que le marqueur ne joue aucun rôle dans
l'assemblage du consommateur.

Le marqueur ne porte délibérément aucun argument. Y mettre l'identifiant et la
catégorie ne supprimerait pas les constantes, puisque les arguments d'un attribut
ne peuvent pas être référencés depuis un autre attribut et que le site
d'utilisation est un attribut ; ils seraient simplement énoncés deux fois, à deux
endroits que rien ne maintient synchronisés. Un marqueur qui dit seulement *ceci
est une règle* n'a pas de seconde copie dont il pourrait diverger.

Le compromis accepté est qu'une règle malformée compile encore. C'est la position
honnête : il n'existe aucune version de ce contrat que le compilateur pourrait
faire respecter, l'analyseur n'est donc pas une commodité posée sur un contrat
vérifié — il est toute la vérification, et le traiter comme optionnel laisserait
le contrat énoncé en prose seulement.

## Alternatives Considered

### Une interface imposant des propriétés `Id` et `Category`

Envisagé parce que c'est la manière ordinaire de dire « ces membres doivent
exister » : c'est vérifié par le compilateur, découvrable dans un IDE, et cela
donne à l'outillage un type sur lequel s'appuyer.

Rejeté parce qu'une propriété ne peut pas être un argument d'attribut. Un type
pourrait implémenter l'interface, satisfaire chaque vérification du compilateur,
et rester inutilisable à la seule position où une référence de règle s'écrit.
Formaliser un contrat qu'on ne peut pas honorer là où cela compte tromperait
précisément les auteurs qu'il devait guider.

### Une classe de base abstraite, ou une instance — `record` ou singleton — par règle

Envisagé parce qu'un type de base pourrait porter du comportement partagé et
donner à l'outillage quelque chose à réfléchir, et parce qu'un modèle à instances
se lit plus naturellement qu'une classe employée comme espace de noms.

Rejeté pour la même raison, aggravée : des propriétés abstraites ne sont pas plus
constantes que des propriétés ordinaires, une instance n'est pas non plus une
valeur de compilation, et une classe statique ne peut pas hériter du tout. Le
modèle devrait abandonner les classes statiques pour gagner un type de base, et
ne gagnerait rien d'utilisable sur un site d'utilisation en échange.

### Mettre l'identifiant et la catégorie sur l'attribut marqueur

Envisagé parce que cela rendrait la déclaration auto-descriptive et donnerait à
l'outillage un seul endroit où lire les données d'une règle, sans aucune attente
structurelle sur les membres du type.

Rejeté parce que cela ne supprime rien. Les constantes doivent toujours exister
pour le site d'utilisation, puisque les arguments d'un attribut ne peuvent pas
être référencés depuis un autre ; l'attribut serait donc un second énoncé des
deux mêmes valeurs sans mécanisme les gardant égales — la duplication que cette
bibliothèque existe pour éliminer, réintroduite dans la déclaration.

### Reconnaître les règles par la seule forme, sans marqueur

Envisagé parce que les constantes sont bel et bien le contrat, et qu'un
appariement par forme seule permettrait à un catalogue de déclarer des règles
sans attribut ni dépendance de paquet.

Rejeté comme comportement par défaut parce que la forme n'est pas distinctive :
des classes statiques avec une constante de chaîne nommée `Id` surviennent pour
des raisons sans rapport, et apparier sur la seule forme ferait signaler par
l'outillage du code dont l'auteur n'a jamais rien choisi. Cela reste utile en
repli documenté pour les auteurs qui le souhaitent, mais c'est le marqueur
explicite qui transforme une forme en déclaration.

## Consequences

### Positive

* Une référence de règle fonctionne à la seule position où elle doit fonctionner,
  et se replie sur le littéral que la plateforme attend.
* L'outillage peut distinguer une règle de n'importe quelle autre classe
  statique, à la déclaration comme à travers une frontière d'assemblage.
* La déclaration énonce un fait une fois : les valeurs vivent dans les
  constantes, et l'attribut dit ce qu'elles sont.

### Negative

* Le contrat est invisible au compilateur, l'analyseur n'est donc pas optionnel —
  un catalogue rédigé sans lui n'est pas vérifié, et aujourd'hui aucun analyseur
  de ce genre n'est livré.
* La forme attendue doit être documentée, parce qu'aucune signature de type ne la
  porte et qu'aucun IDE ne peut proposer de la générer.
* Une règle malformée est un diagnostic plutôt qu'une erreur de build, à moins
  que la build consommatrice ne la promeuve.

### Risks

* Le marqueur est rendu `[Conditional]` — une économie d'apparence plausible,
  puisqu'il ne sert à rien à l'exécution. Chaque catalogue livré en paquet
  deviendrait alors silencieusement invisible : la découverte inter-assemblages
  lit le marqueur dans les métadonnées référencées, un attribut conditionnel n'y
  est pas, et le résultat est aucune règle trouvée, aucun diagnostic signalé et
  aucune erreur nulle part. Atténuation : l'interdiction est enregistrée dans la
  spécification et en contrainte de mainteneur à côté de la déclaration de
  l'attribut, là où celui qui ferait le changement la lit.
* Un auteur déclare des règles sans référencer l'analyseur et livre un catalogue
  que rien n'a vérifié. Atténuation : la documentation d'empaquetage énonce quel
  paquet effectue la vérification ; le risque ne peut pas être refermé depuis ce
  dépôt.

## Follow-up Actions

* Livrer l'analyseur qui valide le contrat structurel ; tant qu'il n'existe pas,
  le contrat ne repose que sur la documentation.
* Documenter la forme attendue dans la documentation destinée aux consommateurs
  du paquet de fondation, puisqu'aucune signature ne l'exprime.
* Maintenir la contrainte « jamais `[Conditional]` » énoncée à côté de la
  déclaration du marqueur et dans la spécification.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — ce
  qui remplit un catalogue bâti sur ce contrat.
* [doc/specification.fr.md](../specification.fr.md) — §3.1, §3.4, §7.1, §8.
* `src/DiagnosticCatalog/DiagnosticRuleAttribute.cs` — le marqueur et sa
  contrainte de mainteneur.
