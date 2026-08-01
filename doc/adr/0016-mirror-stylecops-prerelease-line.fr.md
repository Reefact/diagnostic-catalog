# ADR-0016 | Refléter la ligne de préversion de StyleCop, pas sa version stable périmée

🌍 **Langues :**  
🇬🇧 [English](./0016-mirror-stylecops-prerelease-line.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Un catalogue reflète un paquet d'analyseur amont, et `eng/catalogs.json` résout
`"latest"` vers la dernière version **stable** — un défaut choisi pour qu'un
catalogue ne soit jamais silencieusement épinglé à une préversion.

`StyleCop.Analyzers` met ce défaut en défaut. Il a publié quatre versions stables
dans sa vie (`1.0.0`, `1.0.1`, `1.0.2`, `1.1.118`) contre 63 préversions. La
dernière stable, `1.1.118`, a été publiée en **avril 2019** ; le projet vit sur
`1.2.0-beta` depuis, la plus récente étant `1.2.0-beta.556` de décembre 2023.
C'est cette beta que les projets installent.

`StyleCop.Analyzers 1.2.0-beta` est un métapaquet ne portant aucun assemblage
d'analyseur ; les descripteurs vivent dans `StyleCop.Analyzers.Unstable`, dont
les 24 versions publiées portent toutes un numéro simple à trois ou quatre
segments et aucune étiquette de préversion.

Mesuré entre les deux, `1.1.118` contre `1.2.0.556` :

* quatre règles n'existent que dans la ligne beta — `SA1141`, `SA1142`, `SA1316`,
  `SA1414`, toutes à propos des tuples ;
* **aucune règle n'a été retirée** ;
* une règle diverge : `SA1413` est déclarée sous
  `StyleCop.CSharp.ReadabilityRules` dans la stable et sous
  `StyleCop.CSharp.MaintainabilityRules` dans la beta ;
* un titre diffère par un point final, que le générateur normalise.

La plateforme ne valide jamais la catégorie d'une suppression (§3.2) : une
catégorie fausse ne produit ni erreur, ni avertissement, ni suppression
défaillante, ni test en échec, à aucun point du cycle de vie d'aucun
consommateur.

Les deux autres catalogues ne sont pas concernés : `SonarAnalyzer.CSharp` et
`Microsoft.CodeAnalysis.NetAnalyzers` publient tous deux des versions stables à
un rythme normal.

`DiagnosticCatalog.StyleCop 0.2.0`, qui reflète `1.1.118`, est publié et reste
disponible.

## Decision

Le catalogue StyleCop reflète `StyleCop.Analyzers.Unstable` — la ligne
`1.2.0-beta` — plutôt que la dernière version stable de `StyleCop.Analyzers`.

## Rationale

La valeur d'un catalogue est qu'un consommateur n'ait pas à chercher une valeur,
et cette proposition échoue entièrement si le catalogue décrit une build
différente de celle que son analyseur exécute. `SA1413` est cette défaillance au
présent, pas en théorie : un consommateur sur la beta qui lit
`StyleCopRule.SA1413.Category` depuis un catalogue basé sur la stable obtient une
chaîne que son analyseur ne déclare pas, et rien dans sa build ne le contredit.
C'est précisément l'erreur silencieuse et sans symptôme
qu'[ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md)
existe pour exclure, réapparaissant par le choix de la version plutôt que par le
choix de la source.

Le défaut « stable uniquement » a été choisi pour tenir un catalogue à l'écart
d'une préversion que peu de gens exécutent. Ici il produit l'inverse : il épingle
le catalogue à une version vieille de sept ans que peu de gens exécutent, tandis
que la « préversion » est la version de fait. Appliquer la règle mécaniquement
honorerait sa lettre contre son objet. Le défaut reste juste pour les deux autres
catalogues, et c'est pourquoi ceci est une exception enregistrée pour un éditeur
plutôt qu'un changement de la règle.

Le déplacement est exceptionnellement sûr à faire. Aucune règle ne disparaît,
donc aucune constante n'est supprimée et
[ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) n'est même pas
engagée ; les 193 règles de la stable sont un sous-ensemble des 197 de la beta,
avec des identifiants identiques. Ce qu'un consommateur de `0.2.0` perd en
montant est nul, et ce qu'il gagne est quatre règles et une catégorie corrigée.

Refléter une préversion n'affaiblit pas ce que le paquet promet. Cette promesse
est qu'un identifiant et une catégorie sont ceux que l'analyseur déclare, et
qu'une constante publiée n'est jamais renommée ni supprimée
([ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.fr.md), §23.1).
Cela tient quelle que soit l'étiquette que l'éditeur pose sur la version dont
elles viennent, et `[assembly: CatalogSource]` nomme cette version exactement ;
rien n'est donc caché.

## Alternatives Considered

### Continuer de refléter la dernière stable

Envisagé parce que c'est le défaut du dépôt, que cela n'exige aucune exception
enregistrée, et que livrer un paquet construit depuis la préversion de quelqu'un
invite la question à laquelle cette ADR existe pour répondre.

Rejeté parce que cela décrit une build que presque personne n'exécute, et parce
que ce n'est pas seulement incomplet — `SA1413` la rend fausse, de la seule
manière qu'un consommateur ne peut jamais détecter.

### Publier un second catalogue pour la ligne beta, et garder celui-ci sur la stable

Envisagé parce que chaque paquet dirait alors clairement ce qu'il reflète,
qu'aucun des deux publics ne se verrait demander de changer, et que le générateur
le supporte avec une entrée de manifeste de plus et aucun code nouveau.

Rejeté comme disproportionné à ce qui a été mesuré : quatre règles et une
catégorie séparent les deux lignes. Un second train — sa propre ligne de version,
son changelog, son README, sa release et sa régénération nocturne — est un coût
permanent pour une différence aussi petite, porté pour un éditeur qui a publié
une version stable en sept ans. Le miroir de la stable reste disponible en
`0.2.0` pour qui en a besoin, ce qui est ce qu'un second paquet aurait fourni.

### Encoder plutôt la version reflétée dans la version du paquet

Envisagé parce que cela permettrait aux deux lignes de coexister sous un seul
identifiant de paquet, et rendrait la version reflétée visible sans rien ouvrir.

Rejeté par [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.fr.md) : une
version dérivée de l'amont ne laisse aucun numéro pour un changement propre à ce
dépôt, et le workflow de release accepte exactement trois segments là où
`1.2.0.556` en a quatre.

## Consequences

### Positive

* Le catalogue décrit la build que ses consommateurs exécutent réellement, ce qui
  est toute la proposition.
* La catégorie de `SA1413` devient correcte pour la majorité des utilisateurs.
* Quatre règles qui n'avaient pas de constante en reçoivent une.

### Negative

* Un paquet présenté comme stable reflète la préversion d'un éditeur, ce qui doit
  être expliqué partout où le catalogue est documenté plutôt que d'aller de soi.
* Un consommateur encore sur `1.1.118` doit épingler `0.2.0` plutôt que prendre
  la dernière version.
* `"latest"` se résout désormais dans la ligne `.Unstable` ; le jour où StyleCop
  publiera une vraie `1.2.0` stable, le manifeste devra être repointé à la main —
  rien ne le détecte.

### Risks

* La ligne beta bouge plus vite ou moins prudemment qu'une ligne stable ; une
  régénération pourrait donc porter un changement qu'une version stable aurait
  retenu. Atténuation : la régénération ouvre une pull request portant le diff et
  ne publie rien d'elle-même, exactement comme pour les autres catalogues.
* `StyleCop.Analyzers` livre enfin une stable et le catalogue continue de
  refléter `.Unstable` sans qu'on le remarque. Atténuation : enregistré en action
  de suivi ci-dessous, et l'identifiant du paquet reflété est énoncé dans le
  README propre au catalogue et dans l'en-tête de chaque fichier généré.

## Follow-up Actions

* Réexaminer si `StyleCop.Analyzers` publie une version stable après `1.1.118` :
  la raison de cette exception disparaît avec elle.
* Énoncer dans le README du catalogue quelle ligne amont il reflète et où le
  miroir de la stable reste disponible.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) —
  pourquoi une valeur qui ne peut pas être fausse vaut la peine de lire des
  descripteurs.
* [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.fr.md) — pourquoi la
  version reflétée n'est pas encodée dans la version du paquet.
* [doc/specification.fr.md](../specification.fr.md) — §3.2, §14.1 et §23.1.
* `eng/catalogs.json` — où le choix est exprimé.
