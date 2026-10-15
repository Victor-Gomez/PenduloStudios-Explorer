using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;

namespace PenduloStudiosExplorer.Core.Metadata;

/// <summary>
/// Human-readable names and official chapter information for Runaway's assets.
/// Provides bilingual support (English and Spanish, the game's original development language).
/// </summary>
public static class SceneCatalog
{
    public const string DefaultLanguage = "en";

    public record ChapterInfo(int Number, string TitleEn, string TitleEs);

    public record SceneInfo(string ArchiveName, int Chapter, string NameEn, string NameEs);

    public static readonly IReadOnlyList<ChapterInfo> Chapters =
    [
        new(1, "Wake Me Before Dying", "Despiértame antes de morir"),
        new(2, "The Mysterious Crucifix", "El extraño crucifijo"),
        new(3, "The Great Escape", "La gran evasión"),
        new(4, "Close Encounters of the Fourth Kind", "Encuentros en la cuarta fase"),
        new(5, "Gifts from the Crypt", "La cripta sagrada"),
        new(6, "The Indian, the Nun and the Finger", "El indio, la monja y el dedo"),
    ];

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR1 = Chapters;

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR2 =
    [
        new(1, "Mala Island", "La isla de Mala"),
        new(2, "Surfin' Mala", "Surfeando en Mala"),
        new(3, "Simpler than an Amoeba", "Más simple que una ameba"),
        new(4, "The Guy Who Can Speak to Platypuses", "El hombre que habla con los ornitorrincos"),
        new(5, "Allerton Court", "Allerton Court"),
        new(6, "The Hidden Temple", "El templo oculto"),
    ];

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR3 =
    [
        new(1, "Brian Basco Is Dead", "Brian Basco ha muerto"),
        new(2, "The Art of Running Away", "El arte de escapar"),
        new(3, "A Timely Suicide", "Un suicidio bien oportuno"),
        new(4, "An Unexpected Ally", "Un aliado inesperado"),
        new(5, "Deconstructing Brian", "Brian en todos sus estados"),
        new(6, "The End Is Here", "El final está cerca"),
        new(7, "Epilogue & Flashbacks", "Epílogo y recuerdos"),
    ];

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumber =
        Chapters.ToDictionary(c => c.Number);

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumberR2 =
        ChaptersR2.ToDictionary(c => c.Number);

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumberR3 =
        ChaptersR3.ToDictionary(c => c.Number);

    private static readonly Dictionary<string, SceneInfo> ScenesByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Intro, Main Menu & Developer Logos
            ["RESOURCE.A00"] = new("RESOURCE.A00", 1, "Hospital: Gina's Room (Intro)", "Hospital: Habitación de Gina (Inicio)"),
            ["RESOURCE.A01"] = new("RESOURCE.A01", 1, "Hospital: Gina's Room & Chapter Select", "Hospital: Habitación de Gina y selección de capítulo"),
            ["RESOURCE.A02"] = new("RESOURCE.A02", 0, "Developer Logos: Pendulo Studios & FX", "Logotipos: Pendulo Studios y FX"),
            ["RESOURCE.A03"] = new("RESOURCE.A03", 0, "Main Menu: Chapter Selection Thumbnails", "Menú principal: Miniaturas de selección de capítulo"),

            // Chapter 1: Wake Me Before Dying / Despiértame antes de morir (Hospital)
            ["RESOURCE.B01"] = new("RESOURCE.B01", 1, "Suburbs at Night: Brian's Departure", "Suburbios de noche: Partida de Brian"),
            ["RESOURCE.B02"] = new("RESOURCE.B02", 1, "Hospital: Gina's Room", "Hospital: Habitación de Gina"),
            ["RESOURCE.B06"] = new("RESOURCE.B06", 1, "Hospital: Storage Room & Medical Archives", "Hospital: Almacén y archivo médico"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 1, "Hospital: Gina's Room Bathroom", "Hospital: Baño de la habitación de Gina"),

            // Chapter 2: The Mysterious Crucifix / El extraño crucifijo (Chicago Museum of Natural History)
            ["RESOURCE.D03"] = new("RESOURCE.D03", 2, "Museum: Clive's Office", "Museo: Despacho de Clive"),
            ["RESOURCE.D05"] = new("RESOURCE.D05", 2, "Museum: Restoration Lab", "Museo: Laboratorio de restauración"),
            ["RESOURCE.D06"] = new("RESOURCE.D06", 2, "Museum: Main Lobby (Dinosaur Skeletons)", "Museo: Vestíbulo principal (Esqueletos de dinosaurios)"),
            ["RESOURCE.D07"] = new("RESOURCE.D07", 2, "Museum: Triceratops Display & Janitor Area", "Museo: Expositor del triceratops y rincón de limpieza"),
            ["RESOURCE.D09"] = new("RESOURCE.D09", 2, "Museum: Mayan Exhibition Hall", "Museo: Sala de exposiciones mayas"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 2, "Museum: Thermal Analysis Lab", "Museo: Laboratorio de análisis térmico"),
            ["RESOURCE.D14"] = new("RESOURCE.D14", 2, "Museum: ACME Security Keypad", "Museo: Teclado de seguridad ACME"),

            // Chapter 3: The Great Escape / La gran evasión (Desert & Thugs' Cabin)
            ["RESOURCE.E01"] = new("RESOURCE.E01", 3, "Desert: Region Map & Overview", "Desierto: Mapa de la región y vista general"),
            ["RESOURCE.E02"] = new("RESOURCE.E02", 3, "Desert: Thugs' Cabin (Front Exterior)", "Desierto: Cabaña de los matones (Exterior frontal)"),
            ["RESOURCE.E03"] = new("RESOURCE.E03", 3, "Desert: Thugs' Cabin (Main Room)", "Desierto: Cabaña de los matones (Sala principal)"),
            ["RESOURCE.E04"] = new("RESOURCE.E04", 3, "Desert: Thugs' Cabin (Closet View)", "Desierto: Cabaña de los matones (Vista desde el armario)"),
            ["RESOURCE.E05"] = new("RESOURCE.E05", 3, "Desert: Aircraft Graveyard (Fighter Plane)", "Desierto: Cementerio de aviones (Caza militar)"),
            ["Resource.e06"] = new("Resource.e06", 3, "Desert: Drag Queens' Bus & Camp (Exterior)", "Desierto: Autobús y campamento de las drag queens (Exterior)"),
            ["RESOURCE.E07"] = new("RESOURCE.E07", 3, "Desert: Oil Well & Barrels", "Desierto: Pozo de petróleo y barriles"),
            ["RESOURCE.E08"] = new("RESOURCE.E08", 3, "Desert: Abandoned Train Station & Boxcar", "Desierto: Estación abandonada y vagón de tren"),
            ["RESOURCE.E10"] = new("RESOURCE.E10", 3, "Desert: Thugs' Cabin (Back Scrap Yard)", "Desierto: Cabaña de los matones (Chatarrería trasera)"),
            ["RESOURCE.E11"] = new("RESOURCE.E11", 3, "Desert: Abandoned Train Boxcar (Interior)", "Desierto: Vagón de tren abandonado (Interior)"),
            ["RESOURCE.E13"] = new("RESOURCE.E13", 3, "Desert: Abandoned Hangar (Sandretto Helicopter)", "Desierto: Hangar abandonado (Helicóptero Sandretto)"),
            ["RESOURCE.E14"] = new("RESOURCE.E14", 3, "Desert: Canyon Trail & Telephone Pole", "Desierto: Sendero del cañón y poste de teléfono"),
            ["RESOURCE.E16"] = new("RESOURCE.E16", 3, "Desert: Drag Queens' Bus (Interior, Rear View)", "Desierto: Autobús de las drag queens (Interior, parte trasera)"),
            ["RESOURCE.E17"] = new("RESOURCE.E17", 3, "Desert: Drag Queens' Bus (Interior, Driver View)", "Desierto: Autobús de las drag queens (Interior, puesto de conducción)"),

            // Chapter 4: Close Encounters of the Fourth Kind / Encuentros en la cuarta fase (Douglasville)
            ["RESOURCE.F03"] = new("RESOURCE.F03", 4, "Douglasville: Abandoned Mine Tunnel (Inside)", "Douglasville: Túnel de la mina abandonada (Interior)"),
            ["RESOURCE.F08"] = new("RESOURCE.F08", 4, "Douglasville: Abandoned Mine Entrance", "Douglasville: Entrada a la mina abandonada"),
            ["RESOURCE.F09"] = new("RESOURCE.F09", 4, "Douglasville: Mamá Dorita's House (Front Porch)", "Douglasville: Casa de Mamá Dorita (Porche)"),
            ["RESOURCE.F13"] = new("RESOURCE.F13", 4, "Douglasville: Mamá Dorita's House & Water Well", "Douglasville: Casa de Mamá Dorita y pozo de agua"),
            ["RESOURCE.F18"] = new("RESOURCE.F18", 4, "Douglasville: Main Street", "Douglasville: Calle principal"),
            ["RESOURCE.F19"] = new("RESOURCE.F19", 4, "Douglasville: Ruined Bank", "Douglasville: Banco en ruinas"),
            ["RESOURCE.F20"] = new("RESOURCE.F20", 4, "Douglasville: Sheriff's Office & Jail", "Douglasville: Oficina del sheriff y cárcel"),
            ["RESOURCE.F21"] = new("RESOURCE.F21", 4, "Douglasville: Jail Cell Close-up (Skeleton on Cot)", "Douglasville: Celda de la cárcel (Esqueleto en el catre)"),
            ["RESOURCE.F22"] = new("RESOURCE.F22", 4, "Douglasville: Saloon Balcony Overlooking Main Street", "Douglasville: Balcón del saloon hacia la calle principal"),
            ["RESOURCE.F24"] = new("RESOURCE.F24", 4, "Douglasville: Saloon (Rutger's Greenhouse Corner)", "Douglasville: Saloon (Rincón invernadero de Rutger)"),
            ["RESOURCE.F25"] = new("RESOURCE.F25", 4, "Douglasville: Saloon (Collapsed Cellar & Sculpture)", "Douglasville: Saloon (Sótano derrumbado y escultura)"),
            ["RESOURCE.F26"] = new("RESOURCE.F26", 4, "Douglasville: Derailed Steam Locomotive", "Douglasville: Locomotora de vapor descarrilada"),
            ["RESOURCE.F27"] = new("RESOURCE.F27", 4, "Douglasville: Meteor Crater (Panoramic Overlook)", "Douglasville: Cráter del meteorito (Vista panorámica)"),
            ["RESOURCE.F28"] = new("RESOURCE.F28", 4, "Douglasville: Meteor Crater (Crater Floor)", "Douglasville: Cráter del meteorito (Fondo del cráter)"),
            ["RESOURCE.F29"] = new("RESOURCE.F29", 4, "Douglasville: Joshua's Crater Camp & Synthesizer", "Douglasville: Campamento de Joshua y sintetizador"),
            ["RESOURCE.F30"] = new("RESOURCE.F30", 4, "Douglasville: Saturn's Sculptor Studio & Balcony", "Douglasville: Taller de escultura de Saturno y balcón"),
            ["RESOURCE.F35"] = new("RESOURCE.F35", 4, "Douglasville: Region Map & Overview", "Douglasville: Mapa de la región y vista general"),
            ["RESOURCE.F37"] = new("RESOURCE.F37", 4, "Douglasville: Hotel Lobby & Pool Table", "Douglasville: Vestíbulo del hotel y mesa de billar"),
            ["RESOURCE.F39"] = new("RESOURCE.F39", 4, "Douglasville: Bank Safe Combination Lock Dial", "Douglasville: Dial de combinación de la caja fuerte del banco"),

            // Chapter 5: Gifts from the Crypt & Chapter 6: The Indian, the Nun and the Finger (Douglasville Military Occupation)
            ["RESOURCE.H09"] = new("RESOURCE.H09", 5, "Douglasville: Mamá Dorita's House (Chapter 5/6)", "Douglasville: Casa de Mamá Dorita (Capítulo 5/6)"),
            ["RESOURCE.H10"] = new("RESOURCE.H10", 5, "Douglasville: Mamá Dorita's Hotel Room", "Douglasville: Habitación de hotel de Mamá Dorita"),
            ["Resource.h13"] = new("Resource.h13", 5, "Douglasville: Mamá Dorita's House & Water Well (Chapter 5/6)", "Douglasville: Casa de Mamá Dorita y pozo (Capítulo 5/6)"),
            ["Resource.h18"] = new("Resource.h18", 6, "Military Outpost: Douglasville Main Street (Roadblock)", "Puesto militar: Calle principal de Douglasville (Control)"),
            ["RESOURCE.H19"] = new("RESOURCE.H19", 6, "Military Outpost: Douglasville Bank", "Puesto militar: Banco de Douglasville"),
            ["RESOURCE.H20"] = new("RESOURCE.H20", 6, "Military Outpost: Sheriff's Office & Jail", "Puesto militar: Oficina del sheriff y cárcel"),
            ["RESOURCE.H21"] = new("RESOURCE.H21", 6, "Military Outpost: Jail Cell Close-up", "Puesto militar: Celda de la cárcel"),
            ["RESOURCE.H22"] = new("RESOURCE.H22", 6, "Military Outpost: Saloon Balcony", "Puesto militar: Balcón del saloon"),
            ["Resource.h23"] = new("Resource.h23", 4, "Douglasville: Saloon Piano Mechanism", "Douglasville: Mecanismo del piano del saloon"),
            ["RESOURCE.H24"] = new("RESOURCE.H24", 6, "Military Outpost: Saloon (Rutger's Spot)", "Puesto militar: Saloon (Rincón de Rutger)"),
            ["RESOURCE.H26"] = new("RESOURCE.H26", 6, "Military Outpost: Derailed Locomotive", "Puesto militar: Locomotora descarrilada"),
            ["RESOURCE.H29"] = new("RESOURCE.H29", 6, "Military Outpost: Joshua's Crater Camp", "Puesto militar: Campamento del cráter de Joshua"),
            ["RESOURCE.H30"] = new("RESOURCE.H30", 6, "Military Outpost: Saturn's Studio", "Puesto militar: Taller de Saturno"),
            ["RESOURCE.H35"] = new("RESOURCE.H35", 6, "Military Outpost: Douglasville Map", "Puesto militar: Mapa de Douglasville"),
            ["RESOURCE.H37"] = new("RESOURCE.H37", 6, "Military Outpost: Hotel Lobby", "Puesto militar: Vestíbulo del hotel"),
            ["RESOURCE.H38"] = new("RESOURCE.H38", 6, "Douglasville: Sushi's Hacker Loft & Workshop", "Douglasville: Loft y taller informático de Sushi"),
            ["RESOURCE.H40"] = new("RESOURCE.H40", 6, "Military Camp: Command Tent & Surveillance Terminal", "Campamento militar: Tienda de mando y terminal de vigilancia"),
            ["RESOURCE.H41"] = new("RESOURCE.H41", 6, "Military Camp: Security Keycard Gate", "Campamento militar: Puerta con tarjeta de seguridad"),
            ["RESOURCE.H42"] = new("RESOURCE.H42", 6, "Douglasville: Bank Vault Secret Escape Hatch", "Douglasville: Trampilla secreta de huida de la cámara del banco"),

            // Cutscenes & Finale
            ["RESOURCE.G01"] = new("RESOURCE.G01", 0, "Cutscene: Prologue & Hospital Arrival", "Cinemática: Prólogo y llegada al hospital"),
            ["RESOURCE.G02"] = new("RESOURCE.G02", 0, "Cutscene: Highway Journey & Chicago", "Cinemática: Viaje en carretera y Chicago"),
            ["RESOURCE.G03"] = new("RESOURCE.G03", 0, "Cutscene: Ambush & Desert Capture", "Cinemática: Emboscada y captura en el desierto"),
            ["RESOURCE.G04"] = new("RESOURCE.G04", 0, "Cutscene: The Fall & Hopi Legend", "Cinemática: La caída y leyenda hopi"),
            ["RESOURCE.G05"] = new("RESOURCE.G05", 0, "Cutscene: The Medium Séance & Truth", "Cinemática: Sesión de espiritismo y la verdad"),
            ["RESOURCE.I01"] = new("RESOURCE.I01", 6, "Finale: Showdown & Resolution", "Final: Desenlace y resolución"),
            ["RESOURCE.I03"] = new("RESOURCE.I03", 0, "Outro: End Credits & Epilogue", "Créditos finales y epílogo"),
        };

    private static readonly Dictionary<string, SceneInfo> ScenesByNameR2 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Intro & Prologue
            ["RESOURCE.001"] = new("RESOURCE.001", 1, "Intro: Airplane Interior", "Intro: Interior del avión"),
            ["RESOURCE.I01"] = new("RESOURCE.I01", 0, "Cutscene: Brian & Gina Flight", "Cinemática: Vuelo de Brian y Gina"),
            ["RESOURCE.I02"] = new("RESOURCE.I02", 0, "Cutscene: Cockpit Emergency", "Cinemática: Emergencia en cabina"),

            // Chapter 1: Mala Island / La isla de Mala
            ["RESOURCE.A05"] = new("RESOURCE.A05", 1, "Jungle: Crashed Plane & Lake", "Selva: Avión estrellado y lago"),
            ["RESOURCE.A08"] = new("RESOURCE.A08", 1, "Jungle: Lemur Path", "Selva: Sendero de los lémures"),
            ["RESOURCE.A09"] = new("RESOURCE.A09", 1, "Jungle: Quicksand", "Selva: Arenas movedizas"),
            ["RESOURCE.A10"] = new("RESOURCE.A10", 1, "Jungle: Monkey Tree", "Selva: Árbol del mono"),
            ["RESOURCE.A11"] = new("RESOURCE.A11", 1, "Jungle: Rope Bridge & Gorge", "Selva: Puente colgante y desfiladero"),

            // Chapter 2: Surfin' Mala / Surfeando en Mala
            ["RESOURCE.B01"] = new("RESOURCE.B01", 2, "Beach: Luana's Surf Shack & Bar", "Playa: Caseta de surf de Luana y bar"),
            ["RESOURCE.B03"] = new("RESOURCE.B03", 2, "Beach: Tiki Hut & Pier", "Playa: Cabaña tiki y muelle"),
            ["RESOURCE.B04"] = new("RESOURCE.B04", 2, "Military Camp: Guard Post & Exterior", "Campamento militar: Puesto de guardia y exterior"),
            ["RESOURCE.B04A"] = new("RESOURCE.B04A", 2, "Military Camp: Guard Post (Night)", "Campamento militar: Puesto de guardia (Noche)"),
            ["RESOURCE.B05"] = new("RESOURCE.B05", 2, "Military Camp: Colonel's Office", "Campamento militar: Despacho del coronel"),
            ["RESOURCE.B06"] = new("RESOURCE.B06", 2, "Military Camp: Detention Center", "Campamento militar: Centro de detención"),
            ["RESOURCE.B07"] = new("RESOURCE.B07", 2, "Military Camp: Communications Center", "Campamento militar: Centro de comunicaciones"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 2, "Military Camp: Vehicle Depot", "Campamento militar: Parque de vehículos"),
            ["RESOURCE.B09"] = new("RESOURCE.B09", 2, "Military Camp: Airstrip & Hangars", "Campamento militar: Pista y hangares"),
            ["RESOURCE.B20"] = new("RESOURCE.B20", 2, "Beach: Sunset Overlook", "Playa: Mirador al atardecer"),

            // Chapter 3: Simpler than an Amoeba / Más simple que una ameba
            ["RESOURCE.C01"] = new("RESOURCE.C01", 3, "Yacht: Deck & Ocean", "Yate: Cubierta y océano"),
            ["RESOURCE.C02"] = new("RESOURCE.C02", 3, "Yacht: Bridge & Controls", "Yate: Puente de mando"),
            ["RESOURCE.C03"] = new("RESOURCE.C03", 3, "Yacht: Engine Room", "Yate: Sala de máquinas"),
            ["RESOURCE.C04"] = new("RESOURCE.C04", 3, "Yacht: Rosa's Cabin", "Yate: Camarote de Rosa"),
            ["RESOURCE.C05"] = new("RESOURCE.C05", 3, "Underwater: Sunken Galleon", "Bajo el agua: Galeón hundido"),
            ["RESOURCE.C05B"] = new("RESOURCE.C05B", 3, "Underwater: Galleon Hold", "Bajo el agua: Bodega del galeón"),
            ["RESOURCE.C05C"] = new("RESOURCE.C05C", 3, "Underwater: Coral Reef", "Bajo el agua: Arrecife de coral"),
            ["RESOURCE.C09"] = new("RESOURCE.C09", 3, "Ocean: Sea Floor & Cave", "Océano: Fondo marino y cueva"),
            ["RESOURCE.C10"] = new("RESOURCE.C10", 3, "Yacht: Cargo Hold", "Yate: Bodega de carga"),
            ["RESOURCE.C11"] = new("RESOURCE.C11", 3, "Underwater: Sea Trench", "Bajo el agua: Fosa marina"),
            ["RESOURCE.C12"] = new("RESOURCE.C12", 3, "Yacht: Galley", "Yate: Cocina"),
            ["RESOURCE.C13"] = new("RESOURCE.C13", 3, "Underwater: Submarine Dock", "Bajo el agua: Muelle de submarinos"),
            ["RESOURCE.C14"] = new("RESOURCE.C14", 3, "Underwater: Deep Cavern", "Bajo el agua: Caverna profunda"),
            ["RESOURCE.C15"] = new("RESOURCE.C15", 3, "Underwater: Submarine Interior", "Bajo el agua: Interior del submarino"),
            ["RESOURCE.C16"] = new("RESOURCE.C16", 3, "Underwater: Submarine Airlock", "Bajo el agua: Esclusa del submarino"),
            ["RESOURCE.C17"] = new("RESOURCE.C17", 3, "Underwater: Sunken Ruins", "Bajo el agua: Ruinas sumergidas"),
            ["RESOURCE.C18"] = new("RESOURCE.C18", 3, "Underwater: Deep Trench Overlook", "Bajo el agua: Mirador de la fosa"),
            ["RESOURCE.C19"] = new("RESOURCE.C19", 3, "Underwater: Hidden Alcove", "Bajo el agua: Nicho oculto"),
            ["RESOURCE.C20"] = new("RESOURCE.C20", 3, "Underwater: Ancient Altar", "Bajo el agua: Altar ancestral"),
            ["RESOURCE.C21"] = new("RESOURCE.C21", 3, "Underwater: Escape Hatch", "Bajo el agua: Escotilla de escape"),

            // Chapter 4: The Guy Who Can Speak to Platypuses / El hombre que habla con los ornitorrincos
            ["RESOURCE.D06"] = new("RESOURCE.D06", 4, "Sanctuary: Platypus Lagoon", "Santuario: Laguna de los ornitorrincos"),
            ["RESOURCE.D08"] = new("RESOURCE.D08", 4, "Sanctuary: Joshua's Machine", "Santuario: La máquina de Joshua"),
            ["RESOURCE.D09"] = new("RESOURCE.D09", 4, "Sanctuary: Cliff Path", "Santuario: Sendero del acantilado"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 4, "Sanctuary: Archibald's Hut", "Santuario: Choza de Archibald"),
            ["RESOURCE.D11"] = new("RESOURCE.D11", 4, "Sanctuary: Lookout Point", "Santuario: Mirador"),
            ["RESOURCE.D12"] = new("RESOURCE.D12", 4, "Sanctuary: Rope Bridge", "Santuario: Puente de cuerda"),
            ["RESOURCE.D13"] = new("RESOURCE.D13", 4, "Sanctuary: Platypus Nest", "Santuario: Nido de ornitorrinco"),
            ["RESOURCE.D14"] = new("RESOURCE.D14", 4, "Sanctuary: Mountain Trail", "Santuario: Camino de montaña"),
            ["RESOURCE.D15"] = new("RESOURCE.D15", 4, "Sanctuary: Cave Entrance", "Santuario: Entrada de la cueva"),
            ["RESOURCE.D16"] = new("RESOURCE.D16", 4, "Sanctuary: Abandoned Campsite", "Santuario: Campamento abandonado"),
            ["RESOURCE.D17"] = new("RESOURCE.D17", 4, "Sanctuary: Waterfall & Stream", "Santuario: Cascada y arroyo"),

            // Chapter 5: Allerton Court / Allerton Court
            ["RESOURCE.E01"] = new("RESOURCE.E01", 5, "Allerton Court: Grand Foyer", "Allerton Court: Gran vestíbulo"),
            ["RESOURCE.E02"] = new("RESOURCE.E02", 5, "Allerton Court: Library", "Allerton Court: Biblioteca"),
            ["RESOURCE.E03"] = new("RESOURCE.E03", 5, "Allerton Court: Dining Room", "Allerton Court: Comedor"),
            ["RESOURCE.E04"] = new("RESOURCE.E04", 5, "Allerton Court: Study & Desk", "Allerton Court: Estudio y escritorio"),
            ["RESOURCE.E05"] = new("RESOURCE.E05", 5, "Allerton Court: Cellar & Crypt", "Allerton Court: Sótano y cripta"),
            ["RESOURCE.E07"] = new("RESOURCE.E07", 5, "Allerton Court: Conservatory", "Allerton Court: Invernadero"),
            ["RESOURCE.E08"] = new("RESOURCE.E08", 5, "Allerton Court: Upstairs Gallery", "Allerton Court: Galería superior"),
            ["RESOURCE.E10"] = new("RESOURCE.E10", 5, "Allerton Court: Hidden Vault", "Allerton Court: Cámara acorazada oculta"),
            ["RESOURCE.E11"] = new("RESOURCE.E11", 5, "Allerton Court: Secret Laboratory", "Allerton Court: Laboratorio secreto"),

            // Chapter 6: The Hidden Temple / El templo oculto
            ["RESOURCE.G01"] = new("RESOURCE.G01", 6, "Temple: Outer Courtyard", "Templo: Patio exterior"),
            ["RESOURCE.G02"] = new("RESOURCE.G02", 6, "Temple: Carved Antechamber", "Templo: Antecámara tallada"),
            ["RESOURCE.G03"] = new("RESOURCE.G03", 6, "Temple: Great Pyramid Hall", "Templo: Sala de la gran pirámide"),
            ["RESOURCE.G03B"] = new("RESOURCE.G03B", 6, "Temple: Pyramid Hall (Altar Lit)", "Templo: Sala de la pirámide (Altar encendido)"),
            ["RESOURCE.G04"] = new("RESOURCE.G04", 6, "Temple: Alien Portal", "Templo: Portal alienígena"),
            ["RESOURCE.G05"] = new("RESOURCE.G05", 6, "Temple: Celestial Observatory", "Templo: Observatorio celeste"),
            ["RESOURCE.G06"] = new("RESOURCE.G06", 6, "Temple: Crystal Chamber", "Templo: Cámara de cristal"),
            ["RESOURCE.G07"] = new("RESOURCE.G07", 6, "Temple: Power Conduits", "Templo: Conductos de energía"),
            ["RESOURCE.G08"] = new("RESOURCE.G08", 6, "Temple: Ancient Mechanism", "Templo: Mecanismo ancestral"),
            ["RESOURCE.G10"] = new("RESOURCE.G10", 6, "Spaceship: Command Bridge", "Nave espacial: Puente de mando"),
            ["RESOURCE.G11"] = new("RESOURCE.G11", 6, "Spaceship: Stasis Bay", "Nave espacial: Bahía de éxtasis"),
            ["RESOURCE.G12"] = new("RESOURCE.G12", 6, "Spaceship: Engine Core", "Nave espacial: Núcleo del motor"),
            ["RESOURCE.G13"] = new("RESOURCE.G13", 6, "Spaceship: Observation Deck", "Nave espacial: Cubierta de observación"),

            // Epilogue & Localized Overlays
            ["RESOURCE.H02"] = new("RESOURCE.H02", 6, "Epilogue: Island Shore", "Epílogo: Costa de la isla"),
            ["RESOURCE.H04"] = new("RESOURCE.H04", 6, "Epilogue: Finale Scene", "Epílogo: Escena final"),
            ["RESOURCE.H06"] = new("RESOURCE.H06", 6, "Epilogue: Credits Sequence", "Epílogo: Créditos"),
            ["RESOURCE.SP1"] = new("RESOURCE.SP1", 1, "Character Sprites: Chapter 1", "Personajes: Capítulo 1"),
            ["RESOURCE.SP2"] = new("RESOURCE.SP2", 2, "Character Sprites: Chapter 2", "Personajes: Capítulo 2"),
            ["RESOURCE.SP3"] = new("RESOURCE.SP3", 3, "Character Sprites: Chapter 3", "Personajes: Capítulo 3"),
            ["RESOURCE.SP4"] = new("RESOURCE.SP4", 4, "Character Sprites: Chapter 4", "Personajes: Capítulo 4"),
            ["RESOURCE.SP5"] = new("RESOURCE.SP5", 5, "Character Sprites: Chapter 5", "Personajes: Capítulo 5"),
        };

    private static readonly Dictionary<string, SceneInfo> ScenesByNameR3 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Chapter 1: Brian Basco Is Dead / Brian Basco ha muerto
            ["RESOURCE.A01"] = new("RESOURCE.A01", 1, "Cemetery: Brian's Grave", "Cementerio: Tumba de Brian"),
            ["RESOURCE.A02"] = new("RESOURCE.A02", 1, "Cemetery: Main Gate & Path", "Cementerio: Puerta principal y camino"),
            ["RESOURCE.A03"] = new("RESOURCE.A03", 1, "Cemetery: Crypt Exterior", "Cementerio: Exterior de la cripta"),
            ["RESOURCE.A04"] = new("RESOURCE.A04", 1, "Cemetery: Crypt Interior", "Cementerio: Interior de la cripta"),
            ["RESOURCE.A05"] = new("RESOURCE.A05", 1, "Cemetery: Mausoleum Hall", "Cementerio: Sala del mausoleo"),
            ["RESOURCE.A06"] = new("RESOURCE.A06", 1, "Cemetery: Chapel", "Cementerio: Capilla"),
            ["RESOURCE.A07"] = new("RESOURCE.A07", 1, "Cemetery: Caretaker's Hut", "Cementerio: Caseta del sepulturero"),
            ["RESOURCE.A08"] = new("RESOURCE.A08", 1, "Cemetery: Stone Wall & Trees", "Cementerio: Muro de piedra y árboles"),
            ["RESOURCE.A09"] = new("RESOURCE.A09", 1, "Cemetery: Graveyard Grounds", "Cementerio: Terreno del camposanto"),
            ["RESOURCE.A10"] = new("RESOURCE.A10", 1, "Cemetery: Memorial Walk", "Cementerio: Paseo memorial"),
            ["RESOURCE.A11"] = new("RESOURCE.A11", 1, "Cemetery: Escape Route", "Cementerio: Ruta de escape"),
            ["RESOURCE.A12"] = new("RESOURCE.A12", 1, "Cemetery: Secret Passage", "Cementerio: Pasadizo secreto"),

            // Chapter 2: The Art of Running Away / El arte de escapar
            ["RESOURCE.B00"] = new("RESOURCE.B00", 2, "Happy Dale: Exterior Overview", "Happy Dale: Vista exterior"),
            ["RESOURCE.B01"] = new("RESOURCE.B01", 2, "Happy Dale: Brian's Room", "Happy Dale: Habitación de Brian"),
            ["RESOURCE.B02"] = new("RESOURCE.B02", 2, "Happy Dale: Main Corridor", "Happy Dale: Pasillo principal"),
            ["RESOURCE.B03"] = new("RESOURCE.B03", 2, "Happy Dale: Common Recreation Room", "Happy Dale: Sala de recreo"),
            ["RESOURCE.B04"] = new("RESOURCE.B04", 2, "Happy Dale: Nurse Station", "Happy Dale: Control de enfermería"),
            ["RESOURCE.B05"] = new("RESOURCE.B05", 2, "Happy Dale: Dr. Bennett's Office", "Happy Dale: Despacho del Dr. Bennett"),
            ["RESOURCE.B07"] = new("RESOURCE.B07", 2, "Happy Dale: Security Checkpoint", "Happy Dale: Control de seguridad"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 2, "Happy Dale: Courtyard & Garden", "Happy Dale: Patio y jardín"),
            ["RESOURCE.B10"] = new("RESOURCE.B10", 2, "Happy Dale: Therapy Room", "Happy Dale: Sala de terapia"),
            ["RESOURCE.B11"] = new("RESOURCE.B11", 2, "Happy Dale: Ernie's Room", "Happy Dale: Habitación de Ernie"),
            ["RESOURCE.B12"] = new("RESOURCE.B12", 2, "Happy Dale: Gabbo's Room", "Happy Dale: Habitación de Gabbo"),
            ["RESOURCE.B13"] = new("RESOURCE.B13", 2, "Happy Dale: Marcelo's Quarters", "Happy Dale: Cuarto de Marcelo"),
            ["RESOURCE.B14"] = new("RESOURCE.B14", 2, "Happy Dale: Restrooms", "Happy Dale: Baños"),
            ["RESOURCE.B15"] = new("RESOURCE.B15", 2, "Happy Dale: Storage & Utility", "Happy Dale: Almacén y mantenimiento"),
            ["RESOURCE.B16"] = new("RESOURCE.B16", 2, "Happy Dale: Laundry Room", "Happy Dale: Lavandería"),
            ["RESOURCE.B19"] = new("RESOURCE.B19", 2, "Happy Dale: Ventilation Shaft", "Happy Dale: Conducto de ventilación"),
            ["RESOURCE.B20"] = new("RESOURCE.B20", 2, "Happy Dale: Perimeter Wall & Fence", "Happy Dale: Valla perimetral"),

            // Chapter 3: A Timely Suicide / Un suicidio bien oportuno
            ["RESOURCE.C01"] = new("RESOURCE.C01", 3, "Lake Cabin: Main Living Area", "Cabaña del lago: Salón principal"),
            ["RESOURCE.C02"] = new("RESOURCE.C02", 3, "Lake Cabin: Kitchen & Counter", "Cabaña del lago: Cocina y encimera"),
            ["RESOURCE.C03"] = new("RESOURCE.C03", 3, "Lake Cabin: Bedroom", "Cabaña del lago: Dormitorio"),
            ["RESOURCE.C06"] = new("RESOURCE.C06", 3, "Lake Cabin: Porch & Deck", "Cabaña del lago: Porche y terraza"),
            ["RESOURCE.C07"] = new("RESOURCE.C07", 3, "Lake Shore: Wooden Pier", "Orilla del lago: Muelle de madera"),
            ["RESOURCE.C08"] = new("RESOURCE.C08", 3, "Lake Shore: Boathouse & Shed", "Orilla del lago: Cobertizo de botes"),
            ["RESOURCE.C09"] = new("RESOURCE.C09", 3, "Lake Forest: Wooded Path", "Bosque del lago: Sendero arbolado"),
            ["RESOURCE.C10"] = new("RESOURCE.C10", 3, "Lake Stream: Old Wooden Bridge", "Arroyo del lago: Puente viejo"),
            ["RESOURCE.C11"] = new("RESOURCE.C11", 3, "Lake Forest: Forest Clearing", "Bosque del lago: Claro del bosque"),
            ["RESOURCE.C12"] = new("RESOURCE.C12", 3, "Lake Cabin: Storage Annex", "Cabaña del lago: Anexo de almacenaje"),
            ["RESOURCE.C15"] = new("RESOURCE.C15", 3, "Lake Cabin: Investigation Room", "Cabaña del lago: Sala de investigación"),

            // Chapter 4: An Unexpected Ally / Un aliado inesperado
            ["RESOURCE.D00"] = new("RESOURCE.D00", 4, "Morgue: Basement Hallway", "Depósito de cadáveres: Pasillo del sótano"),
            ["RESOURCE.D01"] = new("RESOURCE.D01", 4, "Morgue: Autopsy Lab", "Depósito de cadáveres: Sala de autopsias"),
            ["RESOURCE.D02"] = new("RESOURCE.D02", 4, "Morgue: Cold Storage & Lockers", "Depósito de cadáveres: Cámaras frigoríficas"),
            ["RESOURCE.D03"] = new("RESOURCE.D03", 4, "Morgue: Incinerator Room", "Depósito de cadáveres: Sala del incinerador"),
            ["RESOURCE.D04"] = new("RESOURCE.D04", 4, "Morgue: Preparation Chamber", "Depósito de cadáveres: Sala de preparación"),
            ["RESOURCE.D05"] = new("RESOURCE.D05", 4, "Morgue: Medical Records Office", "Depósito de cadáveres: Archivo médico"),
            ["RESOURCE.D06"] = new("RESOURCE.D06", 4, "Morgue: Supply Closet", "Depósito de cadáveres: Armario de suministros"),
            ["RESOURCE.D07"] = new("RESOURCE.D07", 4, "Morgue: Loading Bay", "Depósito de cadáveres: Bahía de carga"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 4, "Morgue: Hatch & Air Duct", "Depósito de cadáveres: Escotilla y conducto"),
            ["RESOURCE.D11"] = new("RESOURCE.D11", 4, "Morgue: Emergency Exit", "Depósito de cadáveres: Salida de emergencia"),

            // Chapter 5: Deconstructing Brian / Brian en todos sus estados
            ["RESOURCE.E01"] = new("RESOURCE.E01", 5, "Flashback: Tanton Manor & Camp", "Recuerdo: Mansión Tanton y campamento"),

            // Chapter 6: The End Is Here / El final está cerca
            ["RESOURCE.F01"] = new("RESOURCE.F01", 6, "New York: Dark Alleyways", "Nueva York: Callejones oscuros"),
            ["RESOURCE.F03"] = new("RESOURCE.F03", 6, "New York: Construction Crane & Rooftops", "Nueva York: Grúa de construcción y azoteas"),
            ["RESOURCE.F04"] = new("RESOURCE.F04", 6, "New York: Final Showdown", "Nueva York: Desenlace final"),

            // Epilogue & Cutscenes & Localized Overlays
            ["RESOURCE.H01"] = new("RESOURCE.H01", 7, "Epilogue: Flashback & Revelation", "Epílogo: Recuerdos y revelación"),
            ["RESOURCE.002"] = new("RESOURCE.002", 0, "Cinematic Cutscene Animations", "Animaciones de cinemáticas"),
            ["RESOURCE.SP1"] = new("RESOURCE.SP1", 1, "Spanish Overlay: Chapter 1", "Textos español: Capítulo 1"),
            ["RESOURCE.SP2"] = new("RESOURCE.SP2", 2, "Spanish Overlay: Chapter 2", "Textos español: Capítulo 2"),
            ["RESOURCE.SP3"] = new("RESOURCE.SP3", 3, "Spanish Overlay: Chapter 3", "Textos español: Capítulo 3"),
            ["RESOURCE.SP5"] = new("RESOURCE.SP5", 5, "Spanish Overlay: Chapter 5", "Textos español: Capítulo 5"),
            ["RESOURCE.SP6"] = new("RESOURCE.SP6", 6, "Spanish Overlay: Chapter 6", "Textos español: Capítulo 6"),
            ["RESOURCE.SP7"] = new("RESOURCE.SP7", 7, "Spanish Overlay: Epilogue", "Textos español: Epílogo"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.M01"] = ("Main Theme & Title", "Tema principal y título"),
            ["RESOURCE.M02"] = ("Hospital & Investigation", "Hospital e investigación"),
            ["RESOURCE.M03"] = ("Museum of Natural History", "Museo de Historia Natural"),
            ["RESOURCE.M04"] = ("Desert & Ghost Town", "Desierto y pueblo fantasma"),
            ["RESOURCE.M05"] = ("Hopi Village & Sacred Crypts", "Poblado hopi y criptas sagradas"),
            ["RESOURCE.M06"] = ("Action & Finale", "Acción y final"),
            ["RESOURCE.002"] = ("Cinematic Audio", "Audio de cinemáticas"),
            ["RESOURCE.S01"] = ("Hospital Ambience & SFX", "Hospital: Ambiente y efectos"),
            ["RESOURCE.S02"] = ("Museum Ambience & SFX", "Museo: Ambiente y efectos"),
            ["RESOURCE.S03"] = ("Desert Ambience & SFX", "Desierto: Ambiente y efectos"),
            ["RESOURCE.S04"] = ("Douglasville Ambience & SFX", "Douglasville: Ambiente y efectos"),
            ["RESOURCE.S05"] = ("Pueblo Ambience & SFX", "Pueblo: Ambiente y efectos"),
            ["RESOURCE.S06"] = ("Crypts & Caves Ambience & SFX", "Criptas y cuevas: Ambiente y efectos"),
            ["RESOURCE.S07"] = ("Hopi Village Ambience & SFX", "Poblado hopi: Ambiente y efectos"),
            ["RESOURCE.S08"] = ("Military Camp Ambience & SFX", "Campamento militar: Ambiente y efectos"),
            ["RESOURCE.S09"] = ("Action Ambience & SFX", "Acción: Ambiente y efectos"),
            ["RESOURCE.S10"] = ("Misc Sound Effects", "Efectos de sonido varios"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNamesR2 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.M01"] = ("Mala Island Theme", "Tema de la isla de Mala"),
            ["RESOURCE.M02"] = ("Surfin' Mala Theme", "Tema de Surfeando en Mala"),
            ["RESOURCE.M03"] = ("Under the Sea & Galleon Theme", "Bajo el mar y el galeón"),
            ["RESOURCE.M04"] = ("Platypus Bay Theme", "Bahía de los ornitorrincos"),
            ["RESOURCE.M05"] = ("Allerton Court Manor Theme", "Mansión Allerton Court"),
            ["RESOURCE.M06"] = ("The Hidden Temple & Finale Theme", "El templo oculto y tema final"),
            ["RESOURCE.S00"] = ("Jungle & Nature Ambience", "Ambiente de selva y naturaleza"),
            ["RESOURCE.S01"] = ("Camp & Military Ambience", "Ambiente militar y campamento"),
            ["RESOURCE.S02"] = ("Underwater & Ocean Ambience", "Ambiente oceánico y submarino"),
            ["RESOURCE.S03"] = ("Sanctuary & Island Ambience", "Ambiente del santuario y la isla"),
            ["RESOURCE.S04"] = ("Manor & Indoors Ambience", "Ambiente de la mansión e interiores"),
            ["RESOURCE.S05"] = ("Ancient Temple Ambience", "Ambiente del templo ancestral"),
            ["RESOURCE.S06"] = ("Sound Effects", "Efectos de sonido"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNamesR3 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.S00"] = ("Menu & Interface Sounds", "Sonidos de menú e interfaz"),
            ["RESOURCE.S01"] = ("Chapter 1 Sound Effects", "Efectos de sonido: Capítulo 1"),
            ["RESOURCE.S02"] = ("Chapter 2 Sound Effects", "Efectos de sonido: Capítulo 2"),
            ["RESOURCE.S03"] = ("Chapter 3 Sound Effects", "Efectos de sonido: Capítulo 3"),
            ["RESOURCE.S05"] = ("Chapter 5 Sound Effects", "Efectos de sonido: Capítulo 5"),
            ["RESOURCE.S06"] = ("Chapter 6 Sound Effects", "Efectos de sonido: Capítulo 6"),
            ["RESOURCE.S07"] = ("Epilogue Sound Effects", "Efectos de sonido: Epílogo"),
        };

    private static readonly Dictionary<string, (string En, string Es)> CategoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [VirtualFileSystem.ScenesFolder] = ("Scenes", "Escenas"),
            [VirtualFileSystem.MusicFolder] = ("Music", "Música"),
            [VirtualFileSystem.AmbientFolder] = ("Ambient & SFX", "Sonido ambiente y efectos"),
            [VirtualFileSystem.CinematicFolder] = ("Cinematic Audio", "Audio de cinemáticas"),
            [VirtualFileSystem.VoiceFolder] = ("Voice", "Voces"),
            [VirtualFileSystem.LipSyncFolder] = ("Lip-sync", "Sincronización labial"),
            [VirtualFileSystem.VideoFolder] = ("Video", "Vídeos"),
            [VirtualFileSystem.DialogueFolder] = ("Dialogue", "Diálogos"),
            [VirtualFileSystem.CharactersFolder] = ("Characters", "Personajes"),
            [VirtualFileSystem.GlobalFolder] = ("Global Data", "Datos globales"),
            [VirtualFileSystem.SavegamesFolder] = ("Saved Games", "Partidas guardadas"),
        };

    private static readonly IReadOnlyDictionary<int, ChapterInfo>[] ChaptersPerGame =
        [ChaptersByNumber, ChaptersByNumberR2, ChaptersByNumberR3];

    private static readonly IReadOnlyDictionary<string, SceneInfo>[] ScenesPerGame =
        [ScenesByName, ScenesByNameR2, ScenesByNameR3];

    private static readonly IReadOnlyDictionary<string, (string En, string Es)>[] AudioPerGame =
        [AudioNames, AudioNamesR2, AudioNamesR3];

    public static bool IsSpanish(string? language) =>
        string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) ||
        (language is null && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the official chapter information if known.</summary>
    public static ChapterInfo? GetChapter(int chapterNumber, GameVersion game = GameVersion.Runaway1) =>
        ChaptersPerGame[Math.Clamp((int)game, 0, 2)].GetValueOrDefault(chapterNumber);

    internal static string GetArchiveBaseName(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        int lastSep = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSep >= 0 ? path[(lastSep + 1)..] : path;
    }

    /// <summary>Returns friendly scene title with readable name first and internal name after (e.g. "Ch. 1 (\"Wake Up, Brian!\"): Hospital: Gina's Room (RESOURCE.A01)").</summary>
    public static string GetSceneTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = GetArchiveBaseName(archiveName);
        bool es = IsSpanish(language);
        int gameIdx = Math.Clamp((int)game, 0, 2);
        var scenes = ScenesPerGame[gameIdx];
        var chapters = ChaptersPerGame[gameIdx];

        if (scenes.TryGetValue(baseName, out SceneInfo? info))
        {
            string loc = es ? info.NameEs : info.NameEn;
            if (info.Chapter > 0 && chapters.TryGetValue(info.Chapter, out ChapterInfo? ch))
            {
                string chTitle = es ? ch.TitleEs : ch.TitleEn;
                return $"Ch. {info.Chapter} (\"{chTitle}\"): {loc} ({baseName})";
            }
            return $"{loc} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly title for an audio archive with readable name first (e.g. "Main Theme & Title (RESOURCE.M01)").</summary>
    public static string GetAudioTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = GetArchiveBaseName(archiveName);
        bool es = IsSpanish(language);
        var audio = AudioPerGame[Math.Clamp((int)game, 0, 2)];

        if (audio.TryGetValue(baseName, out var pair))
        {
            string title = es ? pair.Es : pair.En;
            return $"{title} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly name for a top-level virtual filesystem folder.</summary>
    public static string GetCategoryTitle(string categoryName, string? language = null)
    {
        bool es = IsSpanish(language);
        if (CategoryNames.TryGetValue(categoryName, out var pair))
            return es ? pair.Es : pair.En;
        return categoryName;
    }

    private static readonly Dictionary<string, (string En, string Es)> GlobalArchiveTitles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.000"] = ("Global Assets & Fonts", "Recursos globales y fuentes"),
            ["RESOURCE.IFZ"] = ("User Interface & HUD", "Interfaz de usuario y menús"),
            ["RESOURCE.PSE"] = ("Pause Screen Graphics", "Pantalla de pausa"),
            ["RESOURCE.TAB"] = ("Scene Geometry & Waypoints", "Geometría de escenas y rutas"),
            ["RESOURCE.FNT"] = ("Outlined Font Library", "Fuentes con contorno"),
            ["RESOURCE.005"] = ("Chapter Interaction Database", "Base de datos de interacciones"),
            ["RESOURCE.001"] = ("Character Sprite Library", "Biblioteca de sprites de personajes"),
        };

    private static readonly Dictionary<int, (string En, string Es)> HollywoodResidentSounds = new()
    {
        [0x55] = ("Footstep (Wood A)", "Pisada (Madera A)"),
        [0x56] = ("UI Click / Interaction A", "Clic de interfaz / Interacción A"),
        [0x57] = ("Footstep (Stone A)", "Pisada (Piedra A)"),
        [0x58] = ("Door Latch / Click", "Cerrojo / Clic"),
        [0x59] = ("Footstep (Wood B)", "Pisada (Madera B)"),
        [0x5a] = ("Footstep (Stone B)", "Pisada (Piedra B)"),
        [0x5b] = ("Object Pickup / Use", "Recoger / Usar objeto"),
        [0x5c] = ("Page Turn / Paper", "Pasar página / Papel"),
        [0x5d] = ("Footstep (Wood C)", "Pisada (Madera C)"),
        [0x5e] = ("Footstep (Stone C)", "Pisada (Piedra C)"),
        [0x5f] = ("UI Cancel / Back", "Cancelar / Volver"),
        [0x60] = ("Inventory Open", "Abrir inventario"),
        [0x61] = ("Footstep (Stone D)", "Pisada (Piedra D)"),
        [0x62] = ("Inventory Close", "Cerrar inventario"),
    };

    /// <summary>Returns friendly name for a global archive (e.g. "User Interface & HUD (RESOURCE.IFZ)").</summary>
    public static string GetGlobalTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = GetArchiveBaseName(archiveName);
        bool es = IsSpanish(language);

        if (GlobalArchiveTitles.TryGetValue(baseName, out var pair))
        {
            string title = es ? pair.Es : pair.En;
            return $"{title} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly name for a Hollywood Monsters resident sound effect slot (0x55..0x62).</summary>
    public static string GetHollywoodResidentSoundName(int slot, string? language = null)
    {
        bool es = IsSpanish(language);
        if (HollywoodResidentSounds.TryGetValue(slot, out var pair))
            return es ? pair.Es : pair.En;
        return es ? "Efecto residente" : "Resident SFX";
    }

    private sealed class LocalizedNameEntry
    {
        [JsonPropertyName("en")] public string? En { get; set; }
        [JsonPropertyName("es")] public string? Es { get; set; }
    }

    private static readonly Dictionary<(GameVersion Game, int Index), (string En, string Es)> KnownGlobalAssetNames =
        LoadKnownGlobalAssetNames();

    private static readonly Dictionary<(GameVersion Game, string Archive, int Index), (string En, string Es)> KnownSceneAssetNames =
        LoadKnownSceneAssetNames();

    private static Dictionary<(GameVersion Game, int Index), (string En, string Es)> LoadKnownGlobalAssetNames()
    {
        var result = new Dictionary<(GameVersion Game, int Index), (string En, string Es)>();

        void Populate(Stream stream)
        {
            var doc = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, LocalizedNameEntry>>>(stream);
            if (doc is null) return;
            foreach (var (gameStr, entries) in doc)
            {
                if (!Enum.TryParse<GameVersion>(gameStr, ignoreCase: true, out var game)) continue;
                foreach (var (idxStr, loc) in entries)
                {
                    if (int.TryParse(idxStr, out int idx) && loc.En is not null && loc.Es is not null)
                    {
                        result[(game, idx)] = (loc.En, loc.Es);
                    }
                }
            }
        }

        try
        {
            using Stream? stream = typeof(SceneCatalog).Assembly.GetManifestResourceStream("PenduloStudiosExplorer.Core.Resources.known-global-assets.json");
            if (stream is not null)
                Populate(stream);
        }
        catch { }

        try
        {
            string extPath = Path.Combine(AppContext.BaseDirectory, "known-global-assets.json");
            if (File.Exists(extPath))
            {
                using FileStream fs = File.OpenRead(extPath);
                Populate(fs);
            }
        }
        catch { }

        return result;
    }

    private static Dictionary<(GameVersion Game, string Archive, int Index), (string En, string Es)> LoadKnownSceneAssetNames()
    {
        var result = new Dictionary<(GameVersion Game, string Archive, int Index), (string En, string Es)>(new SceneAssetKeyComparer());

        void Populate(Stream stream)
        {
            var doc = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, LocalizedNameEntry>>>>(stream);
            if (doc is null) return;
            foreach (var (gameStr, archives) in doc)
            {
                if (!Enum.TryParse<GameVersion>(gameStr, ignoreCase: true, out var game)) continue;
                foreach (var (archive, entries) in archives)
                {
                    foreach (var (idxStr, loc) in entries)
                    {
                        if (int.TryParse(idxStr, out int idx) && loc.En is not null && loc.Es is not null)
                        {
                            result[(game, archive, idx)] = (loc.En, loc.Es);
                        }
                    }
                }
            }
        }

        try
        {
            using Stream? stream = typeof(SceneCatalog).Assembly.GetManifestResourceStream("PenduloStudiosExplorer.Core.Resources.known-scene-assets.json");
            if (stream is not null)
                Populate(stream);
        }
        catch { }

        try
        {
            string extPath = Path.Combine(AppContext.BaseDirectory, "known-scene-assets.json");
            if (File.Exists(extPath))
            {
                using FileStream fs = File.OpenRead(extPath);
                Populate(fs);
            }
        }
        catch { }

        return result;
    }

    private sealed class SceneAssetKeyComparer : IEqualityComparer<(GameVersion Game, string Archive, int Index)>
    {
        public bool Equals((GameVersion Game, string Archive, int Index) x, (GameVersion Game, string Archive, int Index) y) =>
            x.Game == y.Game && string.Equals(x.Archive, y.Archive, StringComparison.OrdinalIgnoreCase) && x.Index == y.Index;

        public int GetHashCode((GameVersion Game, string Archive, int Index) obj) =>
            HashCode.Combine(obj.Game, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Archive), obj.Index);
    }

    /// <summary>Returns friendly name for a recognised global asset slot in RESOURCE.000 if known.</summary>
    public static string? GetKnownGlobalAssetName(GameVersion game, int index, string? language = null)
    {
        bool es = IsSpanish(language);
        if (KnownGlobalAssetNames.TryGetValue((game, index), out var pair))
            return es ? pair.Es : pair.En;
        return null;
    }

    /// <summary>Returns friendly name for an identified scene archive asset if known.</summary>
    public static string? GetKnownSceneAssetName(GameVersion game, string archiveName, int index, string? language = null)
    {
        string baseName = GetArchiveBaseName(archiveName);
        bool es = IsSpanish(language);
        if (KnownSceneAssetNames.TryGetValue((game, baseName, index), out var pair))
            return es ? pair.Es : pair.En;
        return null;
    }

    /// <summary>Overload defaulting to Runaway 1 for backward compatibility.</summary>
    public static string? GetKnownSceneAssetName(string archiveName, int index, string? language = null, GameVersion game = GameVersion.Runaway1) =>
        GetKnownSceneAssetName(game, archiveName, index, language);

    /// <summary>Formats a scene entry label in the requested language with readable name first and entry ID after.</summary>
    public static string FormatSceneEntryLabel(FsNode entry, string? language = null, GameVersion? game = null)
    {
        GameVersion effectiveGame = game ?? entry.GameVersion ?? GameVersion.Runaway1;
        bool es = IsSpanish(language);
        ImageInfo? img = entry.Image;

        // Check if a specific investigated name exists for this scene entry:
        if (entry.ArchivePath is not null)
        {
            string? specific = GetKnownSceneAssetName(effectiveGame, entry.ArchivePath, entry.EntryIndex, language);
            if (specific is not null)
            {
                if (img is not null)
                {
                    if (entry.Kind == EntryKind.Animation || entry.Kind == EntryKind.CharacterAnimation)
                    {
                        string frameWord = es
                            ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                            : (img.Frames == 1 ? "frame" : "frames");
                        return $"{specific}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
                    }
                    if (entry.Kind == EntryKind.Overlay && (img.X != 0 || img.Y != 0))
                    {
                        string at = es ? "en" : "at";
                        return $"{specific} {img.Width}×{img.Height} {at} {img.X},{img.Y} ({entry.Name})";
                    }
                    return $"{specific} {img.Width}×{img.Height} ({entry.Name})";
                }
                return $"{specific} ({entry.Name})";
            }
        }

        switch (entry.Kind)
        {
            case EntryKind.Background when img is not null:
            {
                if ((effectiveGame == GameVersion.Runaway1 || (entry.GameVersion is null && game is null)) && entry.ArchivePath is not null && GetArchiveBaseName(entry.ArchivePath).Equals("RESOURCE.A03", StringComparison.OrdinalIgnoreCase) && img.Width == 204 && img.Height == 120)
                {
                    string chLabel = entry.EntryIndex switch
                    {
                        0 => es ? "miniatura capítulo 1 (prólogo)" : "chapter 1 (prologue) thumbnail",
                        _ => es ? $"miniatura capítulo {entry.EntryIndex + 1}" : $"chapter {entry.EntryIndex + 1} thumbnail"
                    };
                    return $"{chLabel} {img.Width}×{img.Height} ({entry.Name})";
                }
                if ((effectiveGame == GameVersion.HollywoodMonsters || (entry.GameVersion is null && game is null)) && entry.ArchivePath is not null && GetArchiveBaseName(entry.ArchivePath).Equals("RESOURCE.I04", StringComparison.OrdinalIgnoreCase) && img.Width == 200 && img.Height == 93)
                {
                    string tileLabel = es ? $"miniatura de destino {entry.EntryIndex - 1}" : $"travel destination tile {entry.EntryIndex - 1}";
                    return $"{tileLabel} {img.Width}×{img.Height} ({entry.Name})";
                }
                string desc = es ? (entry.EntryIndex == 0 ? "fondo de escena" : "fondo alternativo") : "background";
                string mask = img.IsMask ? (es ? " (máscara)" : " (mask)") : "";
                return $"{desc} {img.Width}×{img.Height}{mask} ({entry.Name})";
            }

            case EntryKind.Mask when img is not null:
            {
                string desc = es ? "máscara de escena" : "scene mask";
                return $"{desc} {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.Overlay when img is not null:
            {
                if (img.X == 0 && img.Y == 0)
                {
                    string spriteDesc = es ? "capa" : "sprite";
                    return $"{spriteDesc} {img.Width}×{img.Height} ({entry.Name})";
                }
                string desc = es ? "capa" : "overlay";
                string at = es ? "en" : "at";
                return $"{desc} {img.Width}×{img.Height} {at} {img.X},{img.Y} ({entry.Name})";
            }

            case EntryKind.Animation when img is not null:
            {
                string desc = es ? "animación" : "animation";
                string frameWord = es
                    ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                    : (img.Frames == 1 ? "frame" : "frames");
                return $"{desc}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.CharacterAnimation when img is not null:
            {
                string desc = es ? "personaje" : "character animation";
                string frameWord = es
                    ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                    : (img.Frames == 1 ? "frame" : "frames");
                return $"{desc}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.Dialogue:
            {
                string desc = es ? "frase" : "phrase";
                return string.IsNullOrEmpty(entry.Subtitle)
                    ? $"{desc} ({entry.Name})"
                    : $"{desc} ({entry.Name})  \"{entry.Subtitle}\"";
            }

            default:
            {
                if (entry.Size == 1536)
                {
                    string attrWord = es ? "tabla de atributos de escena" : "scene attribute table";
                    return $"{attrWord} ({entry.Name})";
                }
                if (entry.Size == 1792)
                {
                    string lookupWord = es ? "páginas de atributos de escena (7 páginas)" : "scene attribute lookup pages (7 pages)";
                    return $"{lookupWord} ({entry.Name})";
                }
                if ((effectiveGame == GameVersion.HollywoodMonsters || (entry.GameVersion is null && game is null))
                    && entry.Size is >= 14_006 and <= 65_536 && (entry.EntryIndex == 4 || entry.Name.EndsWith("e04", StringComparison.OrdinalIgnoreCase)))
                {
                    string metaWord = es ? "metadatos y acciones de escena" : "scene metadata & interactions";
                    return $"{metaWord} ({entry.Name})";
                }
                if (entry.Size == 335_529)
                {
                    string navWord = es ? "malla de navegación" : "navigation mesh";
                    return $"{navWord} ({entry.Name})";
                }
                if (entry.Size == 43_659)
                {
                    string routeWord = es ? "malla de rutas" : "route mesh";
                    return $"{routeWord} ({entry.Name})";
                }
                if (entry.Size == 704)
                {
                    string corridorWord = es ? "corredor de perspectiva" : "perspective corridor table";
                    return $"{corridorWord} ({entry.Name})";
                }
                if (entry.Size == RouteDecoder.TotalSizeTnbtWaypoints)
                {
                    string navWord = es ? "tabla de navegación" : "waypoint navigation table";
                    return $"{navWord} ({entry.Name})";
                }
                if (entry.Size == RouteDecoder.TotalSizeTnbtHops)
                {
                    string hopWord = es ? "tabla de transiciones de zona" : "zone transition table";
                    return $"{hopWord} ({entry.Name})";
                }
                string dataWord = es ? "datos" : "data";
                return $"{dataWord}, {VirtualFileSystem.FormatSize(entry.Size)} ({entry.Name})";
            }
        }
    }
}
