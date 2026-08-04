# Constellation Build Cheat Sheet

Use `.stars build` to add any of the 88 Modern IAU constellation patterns to the constellation book in your left hand.

## Before You Build

You need:

- A blank normal book or an existing AstraTerra constellation book in your left hand.
- Ink and quill in your inventory.

Use the three-letter IAU code for the most reliable command:

```text
.stars build Ori
```

The current sky culture can also be named explicitly:

```text
.stars build modern_iau:Ori
```

The culture prefix is optional while Modern IAU is the only authored sky culture. Full names work for single-word names such as `Orion`, but the codes also work for names containing spaces.

## Popular Starting Points

```text
.stars build Ori  # Orion
.stars build UMa  # Ursa Major
.stars build UMi  # Ursa Minor
.stars build Cas  # Cassiopeia
.stars build Cyg  # Cygnus
.stars build Sco  # Scorpius
.stars build Sgr  # Sagittarius
.stars build Cru  # Crux
```

## All 88 Constellations

| Code | Constellation | Build command |
|---|---|---|
| `And` | Andromeda | `.stars build And` |
| `Ant` | Antlia | `.stars build Ant` |
| `Aps` | Apus | `.stars build Aps` |
| `Aql` | Aquila | `.stars build Aql` |
| `Aqr` | Aquarius | `.stars build Aqr` |
| `Ara` | Ara | `.stars build Ara` |
| `Ari` | Aries | `.stars build Ari` |
| `Aur` | Auriga | `.stars build Aur` |
| `Boo` | Boötes | `.stars build Boo` |
| `CMa` | Canis Major | `.stars build CMa` |
| `CMi` | Canis Minor | `.stars build CMi` |
| `CVn` | Canes Venatici | `.stars build CVn` |
| `Cae` | Caelum | `.stars build Cae` |
| `Cam` | Camelopardalis | `.stars build Cam` |
| `Cap` | Capricornus | `.stars build Cap` |
| `Car` | Carina | `.stars build Car` |
| `Cas` | Cassiopeia | `.stars build Cas` |
| `Cen` | Centaurus | `.stars build Cen` |
| `Cep` | Cepheus | `.stars build Cep` |
| `Cet` | Cetus | `.stars build Cet` |
| `Cha` | Chamaeleon | `.stars build Cha` |
| `Cir` | Circinus | `.stars build Cir` |
| `Cnc` | Cancer | `.stars build Cnc` |
| `Col` | Columba | `.stars build Col` |
| `Com` | Coma Berenices | `.stars build Com` |
| `CrA` | Corona Australis | `.stars build CrA` |
| `CrB` | Corona Borealis | `.stars build CrB` |
| `Crt` | Crater | `.stars build Crt` |
| `Cru` | Crux | `.stars build Cru` |
| `Crv` | Corvus | `.stars build Crv` |
| `Cyg` | Cygnus | `.stars build Cyg` |
| `Del` | Delphinus | `.stars build Del` |
| `Dor` | Dorado | `.stars build Dor` |
| `Dra` | Draco | `.stars build Dra` |
| `Equ` | Equuleus | `.stars build Equ` |
| `Eri` | Eridanus | `.stars build Eri` |
| `For` | Fornax | `.stars build For` |
| `Gem` | Gemini | `.stars build Gem` |
| `Gru` | Grus | `.stars build Gru` |
| `Her` | Hercules | `.stars build Her` |
| `Hor` | Horologium | `.stars build Hor` |
| `Hya` | Hydra | `.stars build Hya` |
| `Hyi` | Hydrus | `.stars build Hyi` |
| `Ind` | Indus | `.stars build Ind` |
| `LMi` | Leo Minor | `.stars build LMi` |
| `Lac` | Lacerta | `.stars build Lac` |
| `Leo` | Leo | `.stars build Leo` |
| `Lep` | Lepus | `.stars build Lep` |
| `Lib` | Libra | `.stars build Lib` |
| `Lup` | Lupus | `.stars build Lup` |
| `Lyn` | Lynx | `.stars build Lyn` |
| `Lyr` | Lyra | `.stars build Lyr` |
| `Men` | Mensa | `.stars build Men` |
| `Mic` | Microscopium | `.stars build Mic` |
| `Mon` | Monoceros | `.stars build Mon` |
| `Mus` | Musca | `.stars build Mus` |
| `Nor` | Norma | `.stars build Nor` |
| `Oct` | Octans | `.stars build Oct` |
| `Oph` | Ophiuchus | `.stars build Oph` |
| `Ori` | Orion | `.stars build Ori` |
| `Pav` | Pavo | `.stars build Pav` |
| `Peg` | Pegasus | `.stars build Peg` |
| `Per` | Perseus | `.stars build Per` |
| `Phe` | Phoenix | `.stars build Phe` |
| `Pic` | Pictor | `.stars build Pic` |
| `PsA` | Piscis Austrinus | `.stars build PsA` |
| `Psc` | Pisces | `.stars build Psc` |
| `Pup` | Puppis | `.stars build Pup` |
| `Pyx` | Pyxis | `.stars build Pyx` |
| `Ret` | Reticulum | `.stars build Ret` |
| `Scl` | Sculptor | `.stars build Scl` |
| `Sco` | Scorpius | `.stars build Sco` |
| `Sct` | Scutum | `.stars build Sct` |
| `Ser` | Serpens | `.stars build Ser` |
| `Sex` | Sextans | `.stars build Sex` |
| `Sge` | Sagitta | `.stars build Sge` |
| `Sgr` | Sagittarius | `.stars build Sgr` |
| `Tau` | Taurus | `.stars build Tau` |
| `Tel` | Telescopium | `.stars build Tel` |
| `TrA` | Triangulum Australe | `.stars build TrA` |
| `Tri` | Triangulum | `.stars build Tri` |
| `Tuc` | Tucana | `.stars build Tuc` |
| `UMa` | Ursa Major | `.stars build UMa` |
| `UMi` | Ursa Minor | `.stars build UMi` |
| `Vel` | Vela | `.stars build Vel` |
| `Vir` | Virgo | `.stars build Vir` |
| `Vol` | Volans | `.stars build Vol` |
| `Vul` | Vulpecula | `.stars build Vul` |

## After Building

The new pattern is saved in the held book and becomes the selected constellation. These commands are useful next:

```text
.stars list
.stars info selected
.stars name selected <new name>
.stars delete selected
```

Hold the written book in your left hand to display its constellation lines or use it with the Calibrated Astrolabe.
