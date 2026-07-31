using Master.Entity.Enums;
using Master.Entity.TableEntities;

namespace Master.Repository.SeedData;

/// <summary>
/// HSN chapters and SAC groups.
///
/// These are the stable, standard levels of the nomenclature: the 98 HS chapters
/// and the top-level SAC groups. The detailed 4-, 6- and 8-digit codes — roughly
/// thirteen thousand of them — are deliberately NOT hard-coded here. They change
/// with each CBIC notification, and a wrong HSN on a return is a filing error, so
/// they must be loaded from the authoritative file rather than transcribed.
///
/// See HsnSacCsvLoader for importing the full list.
/// </summary>
public static class HsnSacSeed
{
    public static IReadOnlyList<HsnSacCode> Build()
    {
        var rows = new List<HsnSacCode>();
        int id = 1;

        foreach ((string code, string description) in HsnChapters)
        {
            rows.Add(new HsnSacCode
            {
                HsnSacCodeId = id++,
                Code = code,
                CodeType = HsnSacCodeType.Hsn,
                Description = description,
                ChapterCode = code,
                DigitLength = 2,
                DefaultGstRate = null,
                IsActive = true,
            });
        }

        foreach ((string code, string description) in SacGroups)
        {
            rows.Add(new HsnSacCode
            {
                HsnSacCodeId = id++,
                Code = code,
                CodeType = HsnSacCodeType.Sac,
                Description = description,
                ChapterCode = code[..2],
                DigitLength = (byte)code.Length,
                DefaultGstRate = null,
                IsActive = true,
            });
        }

        return rows;
    }

    /// <summary>The 98 Harmonised System chapters. Chapter 77 is reserved and unused.</summary>
    private static readonly (string Code, string Description)[] HsnChapters =
    [
        ("01", "Live animals"),
        ("02", "Meat and edible meat offal"),
        ("03", "Fish and crustaceans, molluscs and other aquatic invertebrates"),
        ("04", "Dairy produce; birds' eggs; natural honey; edible products of animal origin"),
        ("05", "Products of animal origin, not elsewhere specified or included"),
        ("06", "Live trees and other plants; bulbs, roots; cut flowers and ornamental foliage"),
        ("07", "Edible vegetables and certain roots and tubers"),
        ("08", "Edible fruit and nuts; peel of citrus fruit or melons"),
        ("09", "Coffee, tea, mate and spices"),
        ("10", "Cereals"),
        ("11", "Products of the milling industry; malt; starches; inulin; wheat gluten"),
        ("12", "Oil seeds and oleaginous fruits; miscellaneous grains, seeds and fruit"),
        ("13", "Lac; gums, resins and other vegetable saps and extracts"),
        ("14", "Vegetable plaiting materials; vegetable products not elsewhere specified"),
        ("15", "Animal or vegetable fats and oils and their cleavage products"),
        ("16", "Preparations of meat, fish or crustaceans, molluscs or other aquatic invertebrates"),
        ("17", "Sugars and sugar confectionery"),
        ("18", "Cocoa and cocoa preparations"),
        ("19", "Preparations of cereals, flour, starch or milk; pastrycooks' products"),
        ("20", "Preparations of vegetables, fruit, nuts or other parts of plants"),
        ("21", "Miscellaneous edible preparations"),
        ("22", "Beverages, spirits and vinegar"),
        ("23", "Residues and waste from the food industries; prepared animal fodder"),
        ("24", "Tobacco and manufactured tobacco substitutes"),
        ("25", "Salt; sulphur; earths and stone; plastering materials, lime and cement"),
        ("26", "Ores, slag and ash"),
        ("27", "Mineral fuels, mineral oils and products of their distillation"),
        ("28", "Inorganic chemicals; compounds of precious metals and rare-earth metals"),
        ("29", "Organic chemicals"),
        ("30", "Pharmaceutical products"),
        ("31", "Fertilisers"),
        ("32", "Tanning or dyeing extracts; dyes, pigments, paints, varnishes, putty and inks"),
        ("33", "Essential oils and resinoids; perfumery, cosmetic or toilet preparations"),
        ("34", "Soap, organic surface-active agents, washing and lubricating preparations"),
        ("35", "Albuminoidal substances; modified starches; glues; enzymes"),
        ("36", "Explosives; pyrotechnic products; matches; certain combustible preparations"),
        ("37", "Photographic or cinematographic goods"),
        ("38", "Miscellaneous chemical products"),
        ("39", "Plastics and articles thereof"),
        ("40", "Rubber and articles thereof"),
        ("41", "Raw hides and skins (other than furskins) and leather"),
        ("42", "Articles of leather; saddlery and harness; travel goods, handbags"),
        ("43", "Furskins and artificial fur; manufactures thereof"),
        ("44", "Wood and articles of wood; wood charcoal"),
        ("45", "Cork and articles of cork"),
        ("46", "Manufactures of straw, esparto or other plaiting materials; basketware"),
        ("47", "Pulp of wood or other fibrous cellulosic material; recovered paper or paperboard"),
        ("48", "Paper and paperboard; articles of paper pulp, of paper or of paperboard"),
        ("49", "Printed books, newspapers, pictures and other products of the printing industry"),
        ("50", "Silk"),
        ("51", "Wool, fine or coarse animal hair; horsehair yarn and woven fabric"),
        ("52", "Cotton"),
        ("53", "Other vegetable textile fibres; paper yarn and woven fabric of paper yarn"),
        ("54", "Man-made filaments; strip and the like of man-made textile materials"),
        ("55", "Man-made staple fibres"),
        ("56", "Wadding, felt and nonwovens; special yarns; twine, cordage, ropes and cables"),
        ("57", "Carpets and other textile floor coverings"),
        ("58", "Special woven fabrics; tufted textile fabrics; lace; tapestries; embroidery"),
        ("59", "Impregnated, coated, covered or laminated textile fabrics"),
        ("60", "Knitted or crocheted fabrics"),
        ("61", "Articles of apparel and clothing accessories, knitted or crocheted"),
        ("62", "Articles of apparel and clothing accessories, not knitted or crocheted"),
        ("63", "Other made-up textile articles; sets; worn clothing and worn textile articles"),
        ("64", "Footwear, gaiters and the like; parts of such articles"),
        ("65", "Headgear and parts thereof"),
        ("66", "Umbrellas, sun umbrellas, walking sticks, whips, riding crops and parts"),
        ("67", "Prepared feathers and down; artificial flowers; articles of human hair"),
        ("68", "Articles of stone, plaster, cement, asbestos, mica or similar materials"),
        ("69", "Ceramic products"),
        ("70", "Glass and glassware"),
        ("71", "Natural or cultured pearls, precious stones, precious metals; imitation jewellery"),
        ("72", "Iron and steel"),
        ("73", "Articles of iron or steel"),
        ("74", "Copper and articles thereof"),
        ("75", "Nickel and articles thereof"),
        ("76", "Aluminium and articles thereof"),
        ("78", "Lead and articles thereof"),
        ("79", "Zinc and articles thereof"),
        ("80", "Tin and articles thereof"),
        ("81", "Other base metals; cermets; articles thereof"),
        ("82", "Tools, implements, cutlery, spoons and forks, of base metal"),
        ("83", "Miscellaneous articles of base metal"),
        ("84", "Nuclear reactors, boilers, machinery and mechanical appliances; parts thereof"),
        ("85", "Electrical machinery and equipment and parts thereof; sound and TV apparatus"),
        ("86", "Railway or tramway locomotives, rolling stock and parts; track fixtures"),
        ("87", "Vehicles other than railway or tramway rolling stock, and parts thereof"),
        ("88", "Aircraft, spacecraft, and parts thereof"),
        ("89", "Ships, boats and floating structures"),
        ("90", "Optical, photographic, measuring, checking, precision and medical instruments"),
        ("91", "Clocks and watches and parts thereof"),
        ("92", "Musical instruments; parts and accessories of such articles"),
        ("93", "Arms and ammunition; parts and accessories thereof"),
        ("94", "Furniture; bedding, mattresses, cushions; lamps and lighting fittings"),
        ("95", "Toys, games and sports requisites; parts and accessories thereof"),
        ("96", "Miscellaneous manufactured articles"),
        ("97", "Works of art, collectors' pieces and antiques"),
        ("98", "Project imports; laboratory chemicals; passengers' baggage"),
    ];

    /// <summary>Top-level SAC groups. All service codes begin 99.</summary>
    private static readonly (string Code, string Description)[] SacGroups =
    [
        ("99", "Services"),
        ("9954", "Construction services"),
        ("9961", "Services in wholesale trade"),
        ("9962", "Services in retail trade"),
        ("9963", "Accommodation, food and beverage services"),
        ("9964", "Passenger transport services"),
        ("9965", "Goods transport services"),
        ("9966", "Rental services of transport vehicles with operators"),
        ("9967", "Supporting services in transport"),
        ("9968", "Postal and courier services"),
        ("9969", "Electricity, gas, water and other distribution services"),
        ("9971", "Financial and related services"),
        ("9972", "Real estate services"),
        ("9973", "Leasing or rental services without operator"),
        ("9981", "Research and development services"),
        ("9982", "Legal and accounting services"),
        ("9983", "Other professional, technical and business services"),
        ("9984", "Telecommunications, broadcasting and information supply services"),
        ("9985", "Support services"),
        ("9986", "Support services to agriculture, hunting, forestry, fishing and mining"),
        ("9987", "Maintenance, repair and installation services"),
        ("9988", "Manufacturing services on physical inputs owned by others"),
        ("9989", "Other manufacturing services; publishing, printing and reproduction"),
        ("9991", "Public administration and other services to the community"),
        ("9992", "Education services"),
        ("9993", "Human health and social care services"),
        ("9994", "Sewage and waste collection, treatment and disposal services"),
        ("9995", "Services of membership organisations"),
        ("9996", "Recreational, cultural and sporting services"),
        ("9997", "Other services"),
        ("9998", "Domestic services"),
        ("9999", "Services provided by extraterritorial organisations and bodies"),
    ];
}
