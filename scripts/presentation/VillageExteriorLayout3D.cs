using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Village v2 exterior-only additions in metres. Shared by Blender and the preview; no task state.</summary>
public static class VillageExteriorLayout3D
{
    public const string Data = """
    {
      "version": 2,
      "replacedLandmarks": [
        "noticeboard",
        "shop_bench",
        "shop_crates"
      ],
      "props": [
        {
          "id": "Noticeboard",
          "kind": "board",
          "position": [
            -15.5,
            1.7,
            7
          ],
          "size": [
            2.2,
            2.15,
            0.45
          ],
          "solid": true
        },
        {
          "id": "VillageDesk",
          "kind": "desk",
          "position": [
            -19,
            1.3147,
            6.95
          ],
          "size": [
            1.4,
            0.78,
            0.7
          ],
          "solid": true
        },
        {
          "id": "VillageChair",
          "kind": "chair",
          "position": [
            -19,
            1.5,
            6
          ],
          "size": [
            0.48,
            0.95,
            0.5
          ],
          "solid": true
        },
        {
          "id": "PaperCabinet",
          "kind": "cabinet",
          "position": [
            -20.3,
            1.383,
            6.6
          ],
          "size": [
            0.55,
            1.05,
            0.48
          ],
          "solid": true
        },
        {
          "id": "CollectionBox",
          "kind": "mailbox",
          "position": [
            -17.8,
            1.3732,
            6.65
          ],
          "size": [
            0.4,
            1.25,
            0.3
          ],
          "solid": true
        },
        {
          "id": "PlazaBench",
          "kind": "bench",
          "position": [
            -21,
            1.125,
            10.2
          ],
          "size": [
            2.2,
            0.85,
            0.6
          ],
          "solid": true
        },
        {
          "id": "DeliveryTable",
          "kind": "table",
          "position": [
            -7.5,
            1.985,
            2.5
          ],
          "size": [
            1.6,
            0.85,
            0.7
          ],
          "solid": true
        },
        {
          "id": "DryGoods",
          "kind": "sacks",
          "position": [
            -1,
            1.985,
            2.5
          ],
          "size": [
            2,
            0.95,
            1.2
          ],
          "solid": true
        },
        {
          "id": "EmptyCrates",
          "kind": "crates",
          "position": [
            -1,
            1.985,
            3.7
          ],
          "size": [
            1.4,
            0.85,
            0.65
          ],
          "solid": true
        },
        {
          "id": "Handcart",
          "kind": "cart",
          "position": [
            -10.7,
            1.875,
            8.1
          ],
          "size": [
            1,
            1.1,
            1.8
          ],
          "solid": true
        },
        {
          "id": "ShopBench",
          "kind": "bench",
          "position": [
            -9,
            1.8283,
            8.3
          ],
          "size": [
            2.2,
            0.85,
            0.6
          ],
          "solid": true
        },
        {
          "id": "ShopFridge",
          "kind": "fridge",
          "position": [
            -8.7,
            1.985,
            1.95
          ],
          "size": [
            0.85,
            1.65,
            0.55
          ],
          "solid": true
        },
        {
          "id": "ShopPostLeft",
          "kind": "post",
          "position": [
            -9.5,
            1.985,
            3.7
          ],
          "size": [
            0.12,
            2.85,
            0.12
          ],
          "solid": true
        },
        {
          "id": "ShopPostRight",
          "kind": "post",
          "position": [
            0.2,
            1.985,
            3.7
          ],
          "size": [
            0.12,
            2.85,
            0.12
          ],
          "solid": true
        },
        {
          "id": "WorkTable",
          "kind": "workbench",
          "position": [
            -19.3,
            2.75,
            -15.25
          ],
          "size": [
            2.4,
            0.85,
            0.8
          ],
          "solid": true
        },
        {
          "id": "ToolBoard",
          "kind": "toolboard",
          "position": [
            -19.3,
            3.75,
            -15.8
          ],
          "size": [
            2.4,
            1.25,
            0.12
          ],
          "solid": false
        },
        {
          "id": "RepairCabinet",
          "kind": "cabinet",
          "position": [
            -27.8,
            2.75,
            -15.4
          ],
          "size": [
            1.1,
            1.8,
            0.7
          ],
          "solid": true
        },
        {
          "id": "PendingStool",
          "kind": "stool",
          "position": [
            -27.5,
            2.75,
            -13.8
          ],
          "size": [
            0.55,
            0.5,
            0.5
          ],
          "solid": true
        },
        {
          "id": "ReadyStool",
          "kind": "stool",
          "position": [
            -18.2,
            2.75,
            -11.4
          ],
          "size": [
            0.55,
            0.5,
            0.5
          ],
          "solid": true
        },
        {
          "id": "TimberRack",
          "kind": "timber",
          "position": [
            -27.9,
            2.75,
            -12
          ],
          "size": [
            1.5,
            0.55,
            0.65
          ],
          "solid": true
        },
        {
          "id": "SortingBins",
          "kind": "bins",
          "position": [
            -17.6,
            2.75,
            -15.2
          ],
          "size": [
            0.65,
            0.65,
            0.8
          ],
          "solid": true
        },
        {
          "id": "WorkshopPostWest",
          "kind": "post",
          "position": [
            -28.25,
            2.75,
            -10.65
          ],
          "size": [
            0.15,
            3,
            0.15
          ],
          "solid": true
        },
        {
          "id": "WorkshopPostEast",
          "kind": "post",
          "position": [
            -17.75,
            2.75,
            -10.65
          ],
          "size": [
            0.15,
            3,
            0.15
          ],
          "solid": true
        },
        {
          "id": "ShopRoof",
          "kind": "overhead",
          "position": [
            -4,
            4.63,
            2.6
          ],
          "size": [
            11.2,
            0.44,
            2.35
          ],
          "solid": true
        },
        {
          "id": "WorkshopRoof",
          "kind": "overhead",
          "position": [
            -23,
            5.6,
            -13.2
          ],
          "size": [
            11,
            0.45,
            5.6
          ],
          "solid": true
        }
      ],
      "zones": [
        {
          "id": "BoardReading",
          "rect": [
            -17,
            7.5,
            3,
            2.4
          ]
        },
        {
          "id": "DeskVisitors",
          "rect": [
            -20.2,
            7.4,
            2.4,
            2.4
          ]
        },
        {
          "id": "Loading",
          "rect": [
            -9,
            4,
            3,
            3
          ]
        },
        {
          "id": "ShopEntry",
          "rect": [
            -5.4,
            1.8,
            2.8,
            5.5
          ]
        },
        {
          "id": "CooperativeWork",
          "rect": [
            -20.5,
            -14.6,
            2.4,
            2.4
          ]
        }
      ],
      "routes": [
        {
          "id": "board",
          "points": [
            [
              -12,
              15
            ],
            [
              -15.5,
              11
            ],
            [
              -15.5,
              8.9
            ]
          ]
        },
        {
          "id": "desk",
          "points": [
            [
              -15.5,
              11
            ],
            [
              -19,
              10
            ],
            [
              -19,
              8.6
            ]
          ]
        },
        {
          "id": "delivery",
          "points": [
            [
              -4,
              5
            ],
            [
              -5.6,
              6
            ],
            [
              -7.5,
              5.5
            ],
            [
              -7.5,
              3.3
            ]
          ]
        },
        {
          "id": "workbench",
          "points": [
            [
              -29,
              -5
            ],
            [
              -23,
              -14
            ],
            [
              -21.5,
              -12
            ],
            [
              -19.3,
              -13.3
            ]
          ]
        },
        {
          "id": "stool",
          "points": [
            [
              -23,
              -14
            ],
            [
              -25.5,
              -12.5
            ],
            [
              -26.6,
              -13.8
            ]
          ]
        }
      ]
    }
    """;
    public sealed record Prop(string Id,string Kind,float[] Position,float[] Size,bool Solid);
    public sealed record Zone(string Id,float[] Rect);
    public sealed record Definition(int Version,string[] ReplacedLandmarks,Prop[] Props,Zone[] Zones,VillageLayout3D.Route[] Routes);
    public static readonly Definition Layout=JsonSerializer.Deserialize<Definition>(Data,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
}
