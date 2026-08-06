# Empaqueter un catalogue

🌍 **Langues :**  
🇬🇧 [English](./packaging-a-catalogue.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque s'apprête à en publier un. Ce qu'il faut référencer, ce qui se propage à vos
consommateurs que vous l'ayez voulu ou non, et ce que nuget.org fera de votre README.

## Référencez la fondation de la façon ordinaire

```xml
<PackageReference Include="DiagnosticCatalog" Version="1.0.0" />
```

Pas `PrivateAssets="all"`. Vos consommateurs ont besoin que `DiagnosticRuleAttribute` soit résoluble
dans leur propre compilation, et masquer votre dépendance est ce qui le leur retire.

Depuis l'[ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md), cette
unique ligne livre plus que l'attribut : `DiagnosticCatalog` porte les analyseurs `DCAT` et leurs
correctifs à côté de lui. Il n'y a aucun second paquet à référencer, ni pour vous ni pour vos
consommateurs.

Elle est nécessaire, et depuis
l'[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md) elle
n'est pas suffisante. Les analyseurs n'atteignent un compilateur que là où un catalogue les
réclame, et réclamer tient en [un fichier que vous embarquez](#embarquez-lopt-in-qui-fait-vérifier-vos-consommateurs) —
trois lignes, à la section suivante. Sautez-la et vos consommateurs sont **silencieusement** non
vérifiés : leur build réussit et rien ne signale.

> **Une correction, énoncée plutôt que corrigée en douce.** Ce guide affirmait que masquer la
> fondation laisse les analyseurs ne trouver **aucune règle** et ne rien signaler. Ce n'est pas ce qui
> se passe, et c'est désormais asserté plutôt qu'argumenté : avec la fondation absente de la
> compilation d'un consommateur et présente dans les métadonnées du catalogue, `DCAT0006` est quand
> même signalé. Deux mécanismes le font survivre — le pré-filtre admet votre assemblage parce que son
> module *liste* encore `DiagnosticCatalog` dans ses références, et le marqueur est apparié par nom
> pleinement qualifié, si bien qu'un attribut non résoluble est un type d'erreur qui conserve son nom.
> Le test est
> `MarkerRecognitionTests.A_referenced_catalogue_is_found_although_the_consumer_cannot_resolve_the_marker`.
> Ce que masquer la fondation décide désormais est autre chose, ci-dessous : qu'un analyseur y
> tourne ou non.

Alors que coûte réellement `PrivateAssets="all"` ? Trois choses, et la première est pourquoi cette
section est désormais une règle plutôt qu'une préférence :

* **Vos consommateurs ne sont pas vérifiés du tout.** Les analyseurs voyagent dans
  `DiagnosticCatalog` : la référence qui le masque les masque aussi. Mesuré, sous le nom
  `a catalogue hiding the foundation delivers no analyzer either` dans
  `tools/packaging/verify-consumption.sh`.
* **Un consommateur qui déclare ses propres règles ne le peut pas.** `[DiagnosticRule]` ne résout pas
  dans sa source, et il obtient `CS0246` jusqu'à ce qu'il ajoute la fondation à la main — une
  dépendance que votre paquet avait déjà et a refusé de déclarer.
* **Tout ce qui lit votre catalogue par réflexion à l'exécution** — un générateur de documentation, un
  script d'inventaire, `dcat list` sur votre assemblage — rencontre un type d'attribut qu'il ne peut
  pas lier.

Les deux dernières échouent bruyamment, ce qui en faisait naguère un conseil plutôt qu'une règle. La
première, non : une base de code que rien ne vérifie ressemble en tout point à une base de code sans
rien à signaler, et c'est le silence que cette bibliothèque existe pour supprimer.

## Embarquez l'opt-in qui fait vérifier vos consommateurs

Embarquez ce fichier dans votre catalogue sous `build/<votre identifiant de paquet>.props` :

```xml
<Project>
  <PropertyGroup>
    <EnableDiagnosticCatalogAnalyzers Condition="'$(EnableDiagnosticCatalogAnalyzers)' == ''">true</EnableDiagnosticCatalogAnalyzers>
  </PropertyGroup>
</Project>
```

```xml
<ItemGroup>
  <None Include="DiagnosticCatalogOptIn.props"
        Pack="true" PackagePath="build/$(PackageId).props" />
</ItemGroup>
```

**Le nom compte.** NuGet importe `build/<identifiant de paquet>.props` et ignore un fichier nommé
autrement : une faute de frappe ici donne un catalogue qui ne vérifie personne et n'en dit rien.

**Pourquoi c'est votre fichier et non le nôtre.** NuGet importe le dossier `build/` d'un paquet pour
une référence **directe** et pour rien au-delà. C'est le seul endroit de tout le mécanisme où
« quelqu'un a référencé *ceci* » se distingue de « quelqu'un est en aval de ceci », et seul votre
paquet s'y trouve : la fondation est transitive pour vos consommateurs, et transitive à nouveau pour
les leurs, elle ne peut donc pas distinguer les deux. Vos trois lignes sont ce qui empêche une
application référençant une bibliothèque qui vous référence d'être analysée par un catalogue qu'elle
n'a jamais choisi.

La propriété est lue par `buildTransitive/DiagnosticCatalog.targets` dans `DiagnosticCatalog`, où
résident les assemblages d'analyseurs. Vous n'embarquez aucun analyseur à vous, et c'est ce qui
maintient un consommateur de plusieurs catalogues sur exactement une instance d'analyseur, à une
seule version.

**Vos consommateurs peuvent passer outre, dans les deux sens**, et le leur permettre ne vous coûte
rien : un projet posant `EnableDiagnosticCatalogAnalyzers` à `false` garde votre catalogue et décline
l'analyse, et un projet la posant à `true` réclame les vérifications de plus loin qu'une référence
directe. Ni l'un ni l'autre n'est un cas que vous avez à traiter.

Dans ce dépôt, le fichier est [`build/CatalogueAnalyzerOptIn.props`](../../build/CatalogueAnalyzerOptIn.props)
et `Directory.Build.targets` l'embarque dans chaque projet packageable qui dépend de la fondation, si
bien qu'un quatorzième catalogue le porte sans que personne y pense. Hors de ce dépôt, ce sont trois
lignes dans votre `.csproj`.

## Ne pas prendre la dépendance du tout

Si vous préférez livrer un catalogue **sans aucune** dépendance, déclarez le marqueur vous-même :

```csharp
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute { }
}
```

C'est supporté et testé, pas une astuce. Les analyseurs apparient le marqueur par son **nom
pleinement qualifié**, jamais par identité de symbole : votre copie est donc reconnue exactement comme
la vraie. C'est le même motif que PolySharp emploie pour `IsExternalInit`, et
`MarkerRecognitionTests.A_catalogue_declaring_its_own_marker_is_still_analysed` est ce qui le maintient
en état.

Le nom doit être exact — `DiagnosticCatalog.DiagnosticRuleAttribute`, dans cet espace de noms. Un
attribut du même nom court ailleurs appartient à quelqu'un d'autre, et n'est délibérément pas apparié.

`internal` est le bon choix : rien hors de votre assemblage n'a besoin de l'appliquer, et une copie
publique entrerait en collision avec la vraie pour tout consommateur qui référence les deux.

**Ce que la copie retire.** En .NET, l'identité d'un type est son assemblage *plus* son nom : votre
copie et la vraie sont donc deux types sans lien qui se contentent de porter le même nom — invisible
jusqu'à ce que quelque chose lise votre catalogue par réflexion à l'exécution et apparie **par type**,
car `GetCustomAttribute<DiagnosticRuleAttribute>()` lie l'attribut de la fondation, jamais le vôtre, et
rend `null` sur chacune des règles que vous livrez. Apparier sur `GetType().FullName` les retrouve
toutes : c'est ainsi que les analyseurs, `dcat` et les propres `GeneratedCatalogTests` de ce dépôt
lisent un catalogue. Cela mérite une ligne dans votre README, car à la différence de
`PrivateAssets="all"` ci-dessus, cette défaillance-ci est silencieuse : l'outil annonce un catalogue de
zéro règle, indiscernable d'un assemblage qui n'en déclare aucune.

**Et ce qu'elle retire à vos consommateurs.** Les analyseurs voyagent avec la fondation : un
catalogue qui ne dépend de rien ne livre rien qui signale — vos utilisateurs obtiennent les
constantes, et aucun `DCAT0006` sur les littéraux qu'ils n'ont pas encore convertis. Ils peuvent
référencer `DiagnosticCatalog` eux-mêmes pour récupérer la vérification — soit une seconde ligne
pour ce README, car rien d'autre ne le leur dira.

## Ce qui se propage à vos consommateurs

Référencer votre catalogue fait vérifier **vos consommateurs**, et s'arrête là. Chaque ligne
ci-dessous a été mesurée contre une vraie restauration plutôt que lue dans la documentation de
NuGet, dans `tools/packaging/verify-consumption.sh` :

| Qui compile | Les analyseurs tournent |
| --- | --- |
| un projet qui référence votre catalogue | **oui**, si vous avez embarqué l'opt-in |
| un projet qui référence votre catalogue, et vous n'avez rien embarqué | non, en silence |
| un projet qui référence une bibliothèque qui référence votre catalogue | **non** |
| ce même projet, ayant posé `EnableDiagnosticCatalogAnalyzers=true` | oui |
| un projet qui référence votre catalogue avec `EnableDiagnosticCatalogAnalyzers=false` | non ; il garde `[DiagnosticRule]` |
| un projet qui référence votre catalogue, lequel a masqué la fondation par `PrivateAssets="all"` | non, et `[DiagnosticRule]` cesse de résoudre pour lui |

**La troisième ligne est la raison pour laquelle l'opt-in est votre fichier.** Une application qui
référence une bibliothèque ordinaire ayant pris votre catalogue pour ses propres suppressions n'a
choisi ni vous ni les analyseurs, et `DCAT0006` est livré en **erreur** : avant
l'[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md), le
build de cette application s'arrêtait donc sur ses propres suppressions, sans rien dans son propre
fichier projet à montrer du doigt. L'auteur de la bibliothèque tenait le seul levier et n'avait
aucune raison de s'en saisir.

**La dernière ligne n'est pas un renoncement qu'un catalogue peut se permettre.** Un seul paquet
veut dire un seul levier : retenir les analyseurs retient l'attribut avec eux, si bien qu'un
consommateur écrit de la façon ordinaire cesse de compiler au lieu de rester simplement non
vérifié — le `CS0246` que
[le dépannage](troubleshooting.fr.md#cs0246-the-type-or-namespace-name-diagnosticrule-could-not-be-found)
signale déjà. La vérification qui le dit s'appelle
`a catalogue hiding the foundation withholds the attribute assembly`, et son montage consommateur
doit déclarer son propre marqueur pour seulement compiler.

**Une bibliothèque qui référence votre catalogue est vérifiée elle-même** — elle l'a bien choisi. Ce
qu'elle ne fait plus, c'est le transmettre, et elle n'a rien à écrire pour cela.
`PrivateAssets="all"` sur sa propre référence fonctionne toujours et est désormais superflu.

## Le tableau récapitulatif

| Qui vous êtes | Ce que vous référencez | Comment |
| --- | --- | --- |
| **Consommateur** — écrit des suppressions | un catalogue | référence ordinaire ; les vérifications arrivent avec |
| **Consommateur** — veut les vérifications et aucun catalogue | `DiagnosticCatalog` | référence ordinaire |
| **Consommateur** — veut un catalogue sans l'analyse | un catalogue | référence ordinaire, plus `EnableDiagnosticCatalogAnalyzers=false` |
| **Consommateur** — veut les vérifications qu'un catalogue de bibliothèque ne transmet plus | rien de plus | `EnableDiagnosticCatalogAnalyzers=true` |
| **Auteur de catalogue** | `DiagnosticCatalog` | **référence ordinaire**, jamais `PrivateAssets="all"`, **plus l'opt-in ci-dessus** |
| **Auteur de bibliothèque** — a pris un catalogue, ne l'impose pas | ce catalogue | rien ; il ne voyage plus |
| **Auteur d'analyseur** — possède les deux | `DiagnosticCatalog` dans le projet catalogue ; le catalogue dans le projet analyseur | voir [boucler la boucle](first-party-analyzers.fr.md) |

## Votre README est votre page de paquet

`<PackageReadmeFile>` est rendu par nuget.org, et ce rendu a deux contraintes que la plupart des gens
découvrent à leurs dépens.

**Il ne résout aucun lien relatif.**
`[le guide de l'auteur](../../doc/guide/authoring-a-catalogue.en.md)` est un lien mort sur la page du
paquet, aussi correct soit-il dans le dépôt. Pointez vers l'extérieur en adresses absolues :

```markdown
[le guide de l'auteur](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
```

Ce dépôt en avait cinq, en ligne sur des pages publiées, jusqu'à ce qu'un test se mette à les refuser.

**Il n'offre aucun sélecteur de langue.** Un fichier par paquet, dans une langue — ce qui décide
quelle moitié d'un README bilingue un paquet emporte, pas si l'autre moitié existe. Les pages d'ici
sont maintenues sous les noms `README.en.md` et `README.fr.md`, `<PackageReadmeFile>` nomme
l'anglaise, et la bannière qui offre la française est une adresse complète comme chaque autre lien
qu'elles écrivent
([ADR-0034](../adr/0034-pair-every-package-readme-in-english-and-french.fr.md)).

Deux choses que le README d'un catalogue doit porter et que rien d'autre ne dira au lecteur :

* **La version amont qu'il reflète, et sa date de génération.** C'est la première chose dont a besoin
  quiconque évalue le paquet, et une page de paquet n'a aucun voisin à côté d'elle. Dans ce dépôt, le
  générateur l'écrit entre des marqueurs `<!-- mirror:begin -->`, et `DocumentedMirrorTests` fait
  échouer un document dont le bandeau ne correspond pas à l'attribut `CatalogSource` que le générateur
  a écrit — un bandeau que rien ne peut atteindre n'énonce rien. `dcat` écrit dans le README que votre
  dossier de catalogue détient réellement : `README.md` si c'est votre convention, `README.en.md` et
  `README.fr.md` si vous maintenez une paire. Une orthographe que vous ne gardez pas n'est pas signalée.
* **Les autres catalogues que vous publiez, par identifiant de paquet.** Un lecteur arrivé d'une
  recherche voit ce catalogue et rien d'autre.

## L'icône au-dessus

nuget.org affiche l'icône d'un paquet en 128px, au-dessus du titre, dans chaque liste et chaque
résultat de recherche. C'est la première chose que l'on voit de votre paquet et à peu près la
dernière à laquelle on pense en le construisant — et à cette taille, elle tient environ trois
caractères.

Les catalogues d'ici dépensent ces caractères sur le **préfixe des règles que le catalogue
reflète**, jamais sur le nom de l'éditeur. Le badge de StyleCop porte `SA`
plutôt que `SC` parce que `SA1000` est ce qu'un lecteur tape dans `[SuppressMessage(...)]` et que
`SC` n'est tapé par personne ; l'icône répond donc à « ce paquet contient-il ma règle ? » sans qu'on
ouvre la page. La marque elle-même est
[`assets/icon-template.svg`](../../assets/icon-template.svg), où le texte du badge est la seule
chose qu'il reste à modifier.

Trois caractères est ici un plafond, pas un constat : un préfixe plus long est abrégé — `xUnit`
devient `XU`, `MSTEST` devient `MST`. Le texte rétrécit pour dégager les coins de la plaque, si bien
que le mot qui tient exactement est le mot que personne ne peut lire ; mesuré sur les catalogues
publiés ici, un badge de six lettres tombe sous les 5px dans cette liste quand un badge de trois en
tient 9,8. Le document en vigueur est
l'[ADR-0035](../adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.fr.md) ; le plafond
lui-même a d'abord été énoncé par l'[ADR-0033](../adr/0033-cap-the-badge-at-three-letters.fr.md),
que l'ADR-0035 supersède et dont il conserve le plafond, et le choix de ce que dit le badge par
l'[ADR-0032](../adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md) avant lui.

**Et le préfixe peut déjà être pris.** Trois catalogues d'ici reflètent des règles `RS`, donc la
règle ci-dessus ne peut pas donner le même badge aux trois — le sigle ne varie pas, donc des badges
identiques sont des fichiers identiques, ce que `PackageIconTests` refuse. Lorsqu'un préfixe est déjà
porté, le badge du nouveau venu nomme à la place le sujet du paquet qu'il reflète, et le préfixe
reste au catalogue qui le publie déjà : `DiagnosticCatalog.Roslyn` garde `RS`, tandis que
`DiagnosticCatalog.PublicApi` lit `API` et `DiagnosticCatalog.BannedApi` lit `BAN`. C'est la
seconde moitié de l'ADR-0035.

Il vaut la peine de savoir jusqu'où porte la vérification qui l'entoure, car elle est plus étroite
qu'il n'y paraît. `PackageIconTests` fait échouer un catalogue qui ne porte pas son propre
`icon.png`, celui dont l'icône est identique octet pour octet à celle d'un autre catalogue, et celui
qui porte encore la marque sans badge du dépôt. Il ne lit jamais le badge : la distinction est la
propriété qu'il peut affirmer, et ce que disent réellement les lettres repose sur ce gabarit et sur
la revue.

## Ce que l'empaquetage vous donne ici

Pour référence, si vous regardez les projets de ce dépôt : un projet rejoint un train de release en
déclarant `<ReleaseTrain>` dans son propre `.csproj`, et cette unique déclaration est toute
l'appartenance — elle rend le projet empaquetable et lui donne un SBOM SPDX embarqué. Rien ne liste
les projets une seconde fois : un projet renommé ou déplacé ne peut donc pas disparaître
silencieusement de sa propre release.

La règle qui va avec : un projet d'un train ne doit pas porter de `<ProjectReference>` vers un projet
d'un autre, parce que `dotnet pack` estamperait une dépendance vers une version jamais publiée
([ADR-0007](../adr/0007-depend-across-trains-through-published-packages.fr.md)). C'est pourquoi les
catalogues d'ici prennent la fondation en `PackageReference` alors même que sa source est dans le même
dépôt.

## Où aller ensuite

* [**Versionner un catalogue**](versioning-a-catalogue.fr.md) — la règle sur les `const` qui décide de
  ce qu'une release peut et ne peut pas changer.
* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — ce qu'on dira à vos utilisateurs, et quand.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — les trains de release, et comment un catalogue est
  ajouté ici.

---

<div align="center">
<a href="./versioning-a-catalogue.fr.md">← Versionner un catalogue</a> · <a href="./README.fr.md">↑ Table des matières</a>
</div>
