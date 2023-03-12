// MIT License
//
// Copyright (c) 2023 Roland Helmerichs
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;
using FileAccess = Godot.FileAccess;

[Tool]
public class TilemapCreator
{
    private const uint FlippedHorizontallyFlag = 0x80000000;
    private const uint FlippedVerticallyFlag   = 0x40000000;
    private const uint FlippedDiagonallyFlag   = 0x20000000;

    private const string BackgroundColorRectName = "Background Color";
    private const string WarningColor = "Yellow";

    private string _mapOrientation;
    private int _mapWidth;
    private int _mapHeight;
    private int _mapTileWidth;
    private int _mapTileHeight;
    private bool _infinite;
    //private int _parallaxOriginX;
    //private int _parallaxOriginY;
    private string _backgroundColor;
    
    private TileMap _tilemap;
    private float _tilemapOffsetX;
    private float _tilemapOffsetY;
    private TileSet _tileset;
    private Node2D _baseNode;
    private ParallaxBackground _parallaxBackground;
    private ColorRect _background;
    private bool _parallaxLayerExisting;

    private string _basePath;
    private string _baseName;
    private string _encoding;
    private string _compression;
    private bool _mapLayersToTilemaps;
    private int _tmLayerCounter;
    private readonly Array<int> _firstGids = new ();
    private Array _atlasSources;
    private bool _useDefaultFilter;

    private float _isoRot;
    private float _isoSkew;
    private Vector2 _isoScale;

    private int _errorCount;
    private int _warningCount;

    private enum ObjectClass
    {
        Body,
        Area,
        Navigation,
        Occluder,
        Line,
        Path,
        Polygon,
        Unknown
    }

    public int GetErrorCount()
    {
        return _errorCount;
    }

    public int GetWarningCount()
    {
        return _warningCount;
    }
    
    public void SetMapLayersToTilemaps(bool value)
    {
        _mapLayersToTilemaps = value;
    }

    public void SetUseDefaultFilter(bool value)
    {
        _useDefaultFilter = value;
    }

    public TileSet GetTileset()
    {
        return _tileset;
    }

    public Node2D Create(string sourceFile)
    {
        _basePath = sourceFile.GetBaseDir();
        var baseDictionary = DictionaryBuilder.GetDictionary(sourceFile);
        _mapOrientation = (string)baseDictionary.GetValueOrDefault("orientation", "orthogonal");
        _mapWidth = (int)baseDictionary.GetValueOrDefault("width", 0);
        _mapHeight = (int)baseDictionary.GetValueOrDefault("height", 0);
        _mapTileWidth = (int)baseDictionary.GetValueOrDefault("tilewidth", 0);
        _mapTileHeight = (int)baseDictionary.GetValueOrDefault("tileheight", 0);
        _infinite = (bool)baseDictionary.GetValueOrDefault("infinite", false);
        //_parallaxOriginX = (int)baseDictionary.GetValueOrDefault("parallaxoriginx", 0);
        //_parallaxOriginY = (int)baseDictionary.GetValueOrDefault("parallaxoriginy", 0);
        _backgroundColor = (string)baseDictionary.GetValueOrDefault("backgroundcolor", "");

        if (baseDictionary.ContainsKey("tilesets"))
        {
            var tileSets = (Array<Dictionary>)baseDictionary["tilesets"];
            foreach (var tileSet in tileSets)
                _firstGids.Add((int)tileSet["firstgid"]);
            var tilesetCreator = new TilesetCreator();
            tilesetCreator.SetBasePath(sourceFile);
            tilesetCreator.SetMapTileSize(new Vector2I(_mapTileWidth, _mapTileHeight));
            _tileset = tilesetCreator.CreateFromDictionaryArray(tileSets);
            _errorCount = tilesetCreator.GetErrorCount();
            _warningCount = tilesetCreator.GetWarningCount();
            _atlasSources = tilesetCreator.GetRegisteredAtlasSources();
        }
        // If tileset still null create an empty one
        _tileset ??= new TileSet();
        _tileset.TileSize = new Vector2I(_mapTileWidth, _mapTileHeight);
        switch (_mapOrientation)
        {
            case "isometric":
                _tileset.TileShape = TileSet.TileShapeEnum.Isometric;
                _tileset.TileLayout = TileSet.TileLayoutEnum.DiamondDown;
                break;
            case "staggered":
            {
                var staggerAxis = (string)baseDictionary.GetValueOrDefault("staggeraxis", "y");
                var staggerIndex = (string)baseDictionary.GetValueOrDefault("staggerindex", "odd");
                _tileset.TileShape = TileSet.TileShapeEnum.Isometric;
                _tileset.TileLayout = staggerIndex == "odd" ? TileSet.TileLayoutEnum.Stacked : TileSet.TileLayoutEnum.StackedOffset;
                _tileset.TileOffsetAxis = staggerAxis == "x" ? TileSet.TileOffsetAxisEnum.Vertical : TileSet.TileOffsetAxisEnum.Horizontal;
                break;
            }
            case "hexagonal":
            {
                var staggerAxis = (string)baseDictionary.GetValueOrDefault("staggeraxis", "y");
                var staggerIndex = (string)baseDictionary.GetValueOrDefault("staggerindex", "odd");
                _tileset.TileShape = TileSet.TileShapeEnum.Hexagon;
                _tileset.TileLayout = staggerIndex == "odd" ? TileSet.TileLayoutEnum.Stacked : TileSet.TileLayoutEnum.StackedOffset;
                _tileset.TileOffsetAxis = staggerAxis == "x" ? TileSet.TileOffsetAxisEnum.Vertical : TileSet.TileOffsetAxisEnum.Horizontal;
                break;
            }
        }
        
        _tmLayerCounter = 0;
        
        _baseNode = new Node2D();
        _baseName = sourceFile.GetFile().GetBaseName();
        _baseNode.Name = _baseName;
        _parallaxBackground = new ParallaxBackground();
        _baseNode.AddChild(_parallaxBackground);
        _parallaxBackground.Name = _baseName + " (PBG)";
        _parallaxBackground.Owner = _baseNode;
        if (_backgroundColor != "")
        {
            _background = new ColorRect();
            _background.Color = new Color(_backgroundColor);
            _background.Size = new Vector2(_mapWidth * _mapTileWidth, _mapHeight * _mapTileHeight);
            _baseNode.AddChild(_background);
            _background.Name = BackgroundColorRectName;
            _background.Owner = _baseNode;
        }

        if (baseDictionary.ContainsKey("layers"))
            foreach (var layer in (Array<Dictionary>)baseDictionary["layers"])
                HandleLayer(layer, _baseNode);

        if (_parallaxBackground.GetChildCount() == 0)
            _baseNode.RemoveChild(_parallaxBackground);

        if (_baseNode.GetChildCount() > 1) return _baseNode;

        var ret = (Node2D)_baseNode.GetChild(0);
        ret.Name = _baseName;
        return ret;
    }

    private void HandleLayer(Dictionary layer, Node parent)
    {
        //var layerId = (int)layer["id"];
        var layerOffsetX = (float)layer.GetValueOrDefault("offsetx", 0);
        var layerOffsetY = (float)layer.GetValueOrDefault("offsety", 0);
        var layerOpacity = (float)layer.GetValueOrDefault("opacity", 1.0f);
        var layerVisible = (bool)layer.GetValueOrDefault("visible", true);
        _encoding = (string)layer.GetValueOrDefault("encoding", "csv");
        _compression = (string)layer.GetValueOrDefault("compression", "");
        var layertype = (string)layer.GetValueOrDefault("type", "tilelayer");
        var tintColor = (string)layer.GetValueOrDefault("tintcolor", "#ffffff");

        if (layertype != "tilelayer" && !_mapLayersToTilemaps)
        {
            _tilemap = null;
            _tmLayerCounter = 0;
        }

        switch (layertype)
        {
            case "tilelayer":
            {
                if (_mapOrientation == "isometric")
                    layerOffsetX += _mapTileWidth * (_mapHeight / 2.0f - 0.5f);
                if (_mapLayersToTilemaps)
                {
                    _tilemap = new TileMap();
                    _tilemap.Name = (string)layer["name"];
                    _tilemap.Visible = layerVisible;
                    _tilemap.Position = new Vector2(layerOffsetX, layerOffsetY);
                    if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                        _tilemap.Modulate = new Color(tintColor, layerOpacity);
                    _tilemap.TileSet = _tileset;
                    HandleParallaxes(parent, _tilemap, layer);
                    if (_mapOrientation is "isometric" or "staggered")
                    {
                        _tilemap.YSortEnabled = true;
                        _tilemap.SetLayerYSortEnabled(0, true);
                    }
                }
                else
                {
                    if (_tilemap == null)
                    {
                        _tilemap = new TileMap();
                        _tilemap.Name = (string)layer["name"];
                        _tilemap.RemoveLayer(0);
                        HandleParallaxes(parent, _tilemap, layer);
                        _tilemapOffsetX = layerOffsetX;
                        _tilemapOffsetY = layerOffsetY;
                        _tilemap.Position = new Vector2(layerOffsetX, layerOffsetY);
                        if (_mapOrientation == "isometric")
                            _tilemap.YSortEnabled = true;
                    }
                    else
                        _tilemap.Name += "|" + (string)layer["name"];
                    _tilemap.TileSet ??= _tileset;
                    _tilemap.AddLayer(_tmLayerCounter);
                    _tilemap.SetLayerName(_tmLayerCounter, (string)layer["name"]);
                    _tilemap.SetLayerEnabled(_tmLayerCounter, layerVisible);
                    if (_mapOrientation is "isometric" or "staggered")
                        _tilemap.SetLayerYSortEnabled(_tmLayerCounter, true);
                    if (Math.Abs(layerOffsetX - _tilemapOffsetX) > 0.01f || Math.Abs(layerOffsetY - _tilemapOffsetY) > 0.01f)
                    {
                        GD.PrintRich($"[color={WarningColor}]Godot 4 has no tilemap layer offsets -> switch off 'use_tilemap_layers'[/color]");
                        _warningCount++;
                    }
                    if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                        _tilemap.SetLayerModulate(_tmLayerCounter, new Color(tintColor, layerOpacity));
                }
                if (!_useDefaultFilter)
                    _tilemap.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

                if (_infinite && layer.ContainsKey("chunks"))
                {
                    // Chunks
                    foreach (var chunk in (Array<Dictionary>)layer["chunks"])
                    {
                        var offsetX = (int)chunk["x"];
                        var offsetY = (int)chunk["y"];
                        var chunkWidth = (int)chunk["width"];
                        var chunkData = HandleData(chunk["data"]);
                        if (chunkData != null)
                            CreateMapFromData(chunkData, offsetX, offsetY, chunkWidth);
                    }
                }
                else if (layer.ContainsKey("data"))
                {
                    // Data
                    var data = HandleData(layer["data"]);
                    if (data != null)
                        CreateMapFromData(data, 0, 0, _mapWidth);
                }

                if (layer.ContainsKey("properties"))
                    HandleProperties(_tilemap, (Array<Dictionary>)layer["properties"]);
              
                if (!_mapLayersToTilemaps)
                    _tmLayerCounter++;

                break;
            }
            case "objectgroup":
            {
                var layerNode = new Node2D();
                HandleParallaxes(parent, layerNode, layer);

                if (layer.ContainsKey("name"))
                    layerNode.Name = (string)layer["name"];
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    layerNode.Modulate = new Color(tintColor, layerOpacity);
                layerNode.Visible = (bool)layer.GetValueOrDefault("visible", true);
                var layerPosX = (float)layer.GetValueOrDefault("x", 0.0f);
                var layerPosY = (float)layer.GetValueOrDefault("y", 0.0f);
                layerNode.Position = new Vector2(layerPosX + layerOffsetX, layerPosY + layerOffsetY);
                if (!_useDefaultFilter)
                    layerNode.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                if (_mapOrientation is "isometric" or "staggered")
                    layerNode.YSortEnabled = true;

                if (layer.ContainsKey("objects"))
                    foreach (var obj in (Array<Dictionary>)layer["objects"])
                        HandleObject(obj, layerNode, _tileset, Vector2.Zero);

                if (layer.ContainsKey("properties"))
                    HandleProperties(layerNode, (Array<Dictionary>)layer["properties"]);
                
                break;
            }
            case "group":
            {
                var groupNode = new Node2D();
                HandleParallaxes(parent, groupNode, layer);
                groupNode.Name = (string)layer.GetValueOrDefault("name", "group");
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    groupNode.Modulate = new Color(tintColor, layerOpacity);
                groupNode.Visible = (bool)layer.GetValueOrDefault("visible", true);
                var layerPosX = (float)layer.GetValueOrDefault("x", 0.0f);
                var layerPosY = (float)layer.GetValueOrDefault("y", 0.0f);
                groupNode.Position = new Vector2(layerPosX + layerOffsetX, layerPosY + layerOffsetY);
                // if (!_useDefaultFilter)
                //     groupNode.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

                foreach (Dictionary childLayer in (Array)layer["layers"])
                    HandleLayer(childLayer, groupNode);

                if (layer.ContainsKey("properties"))
                    HandleProperties(groupNode, (Array<Dictionary>)layer["properties"]);
                
                break;
            }
            case "imagelayer":
            {
                var textureRect = new TextureRect();
                HandleParallaxes(parent, textureRect, layer);
                
                textureRect.Name = (string)layer.GetValueOrDefault("name", "image");
                textureRect.Position = new Vector2(layerOffsetX, layerOffsetY);

                var imagewidth = (int)layer.GetValueOrDefault("imagewidth", 0);
                var imageheight = (int)layer.GetValueOrDefault("imageheight", 0);
                textureRect.Size = new Vector2(imagewidth, imageheight);
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    textureRect.Modulate = new Color(tintColor, layerOpacity);
                textureRect.Visible = layerVisible;

                // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
                var texturePath = (string)layer["image"];
                if (!FileAccess.FileExists(texturePath))
                    texturePath = _basePath.GetBaseDir().PathJoin((string)layer["image"]);
                if (!FileAccess.FileExists(texturePath))
                    texturePath = _basePath.PathJoin((string)layer["image"]);
                if (FileAccess.FileExists(texturePath))
                {
                    var exists = ResourceLoader.Exists(texturePath, "Image");
                    if (exists)
                        textureRect.Texture = (Texture2D)ResourceLoader.Load(texturePath, "Image");
                    else
                    {
                        var image = Image.LoadFromFile(texturePath);
                        textureRect.Texture = ImageTexture.CreateFromImage(image);
                    }
                }
                else
                {
                    GD.PrintErr($"ERROR: Image file '{(string)layer["image"]}' not found.");
                    _errorCount++;
                }

                if (layer.ContainsKey("properties"))
                    HandleProperties(textureRect, (Array<Dictionary>)layer["properties"]);
                
                break;
            }
        }
    }

    private void HandleParallaxes(Node parent, Node layerNode, Dictionary layerDict)
    {
        if (layerDict.ContainsKey("parallaxx") || layerDict.ContainsKey("parallaxy"))
        {
            if (!_parallaxLayerExisting)
            {
                _background?.Reparent(_parallaxBackground);
                _parallaxLayerExisting = true;
            }

            var parX = (float)layerDict.GetValueOrDefault("parallaxx", 1.0f);
            var parY = (float)layerDict.GetValueOrDefault("parallaxy", 1.0f);
            var parallaxNode = new ParallaxLayer();
            _parallaxBackground.AddChild(parallaxNode);
            parallaxNode.Owner = _baseNode;
            var pxName = (string)layerDict.GetValueOrDefault("name", "");
            parallaxNode.Name = (pxName != "") ? pxName + " (PL)" : "ParallaxLayer";
            parallaxNode.MotionScale = new Vector2(parX, parY);
            parallaxNode.AddChild(layerNode);
            layerNode.Owner = _baseNode;
        }
        else
        {
            parent.AddChild(layerNode);
            layerNode.Owner = _baseNode;
        }
    }
    
    private Array<uint> HandleData(Variant data)
    {
        var ret = new Array<uint>();
        switch (_encoding)
        {
            case "csv":
            {
                foreach (var cell in (Array)data)
                    ret.Add((uint)cell);
                break;
            }
            case "base64":
            {
                var bytes = Convert.FromBase64String((string)data);
                if (_compression != "")
                {
                    var memoryStream = new MemoryStream(bytes);
                    var memoryStreamOutput = new MemoryStream();
                    switch (_compression)
                    {
                        case "gzip":
                        {
                            var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                            gZipStream.CopyTo(memoryStreamOutput);
                            break;
                        }
                        case "zlib":
                        {
                            var zLibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
                            zLibStream.CopyTo(memoryStreamOutput);
                            break;
                        }
                        default:
                            GD.PrintErr($"Decompression for type '{_compression}' not yet implemented.");
                            _errorCount++;
                            return null;
                    }
                    bytes = memoryStreamOutput.ToArray();
                }
                for (var i = 0; i < bytes.Length / sizeof(uint); i++)
                    ret.Add(BitConverter.ToUInt32(bytes, i * sizeof(uint)));
                break;
            }
        }

        return ret;
    }

    private void CreatePolygonsOnAlternativeTiles(TileData sourceData, TileData targetData, int altId)
    {
        var flippedH = (altId & 1) > 0;
        var flippedV = (altId & 2) > 0;
        var flippedD = (altId & 4) > 0;
        var origin = sourceData.TextureOrigin;
        var physicsLayersCount = _tileset.GetPhysicsLayersCount();
        for (var layerId = 0; layerId < physicsLayersCount; layerId++)
        {
            var collisionPolygonsCount = sourceData.GetCollisionPolygonsCount(layerId);
            for (var polygonId = 0; polygonId < collisionPolygonsCount; polygonId++)
            {
                var pts = sourceData.GetCollisionPolygonPoints(layerId, polygonId);
                var ptsNew = new Vector2[pts.Length];
                var i = 0;
                foreach (var pt in pts)
                {
                    ptsNew[i] = pt + origin;
                    if (flippedD)
                        (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                    if (flippedH)
                        ptsNew[i].X = -ptsNew[i].X;
                    if (flippedV)
                        ptsNew[i].Y = -ptsNew[i].Y;
                    ptsNew[i] -= targetData.TextureOrigin;
                    i++;
                }
                targetData.AddCollisionPolygon(layerId);
                targetData.SetCollisionPolygonPoints(layerId, polygonId, ptsNew);
            }
        }

        var navigationLayersCount = _tileset.GetNavigationLayersCount();
        for (var layerId = 0; layerId < navigationLayersCount; layerId++)
        {
            var navP = sourceData.GetNavigationPolygon(layerId);
            if (navP == null) continue;
            var pts = navP.GetOutline(0);
            var ptsNew = new Vector2[pts.Length];
            var i = 0;
            foreach (var pt in pts)
            {
                ptsNew[i] = pt + origin;
                if (flippedD)
                    (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                if (flippedH)
                    ptsNew[i].X = -ptsNew[i].X;
                if (flippedV)
                    ptsNew[i].Y = -ptsNew[i].Y;
                ptsNew[i] -= targetData.TextureOrigin;
                i++;
            }
            var navigationPolygon = new NavigationPolygon();
            navigationPolygon.AddOutline(ptsNew);
            navigationPolygon.MakePolygonsFromOutlines();
            targetData.SetNavigationPolygon(layerId, navigationPolygon);
        }

        var occlusionLayersCount = _tileset.GetOcclusionLayersCount();
        for (var layerId = 0; layerId < occlusionLayersCount; layerId++)
        {
            var occ = sourceData.GetOccluder(layerId);
            if (occ == null) continue;
            var pts = occ.Polygon;
            var ptsNew = new Vector2[pts.Length];
            var i = 0;
            foreach (var pt in pts)
            {
                ptsNew[i] = pt + origin;
                if (flippedD)
                    (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                if (flippedH)
                    ptsNew[i].X = -ptsNew[i].X;
                if (flippedV)
                    ptsNew[i].Y = -ptsNew[i].Y;
                ptsNew[i] -= targetData.TextureOrigin;
                i++;
            }
            var occluderPolygon = new OccluderPolygon2D();
            occluderPolygon.Polygon = ptsNew;
            targetData.SetOccluder(layerId, occluderPolygon);
        }
    }
    
    private void CreateMapFromData(Array<uint> layerData, int offsetX, int offsetY, int mapWidth)
    {
        var cellCounter = -1;
        foreach (var cell in layerData)
        {
            cellCounter++;
            var intId = cell & 0xFFFFFFFF;
            var flippedH = (intId & FlippedHorizontallyFlag) > 0;
            var flippedV = (intId & FlippedVerticallyFlag) > 0;
            var flippedD = (intId & FlippedDiagonallyFlag) > 0;
            var gid = (int)(intId & 0x0FFFFFFF);
            if (gid <= 0) continue;
            var cellCoords = new Vector2I(cellCounter % mapWidth + offsetX, cellCounter / mapWidth + offsetY);

            var sourceId = GetMatchingSourceId(gid);
            var firstGidId = GetFirstGidIndex(gid);
            if (firstGidId > sourceId)
                sourceId = firstGidId;
            // Should not be the case, but who knows...
            if (sourceId < 0) continue;

            TileSetAtlasSource atlasSource;
            if (_tileset.HasSource(sourceId))
                atlasSource = (TileSetAtlasSource)_tileset.GetSource(sourceId);
            else
                continue;
            var atlasWidth = atlasSource.GetAtlasGridSize().X;
            if (atlasWidth <= 0) continue;
            
            var effectiveGid = gid - _firstGids[GetFirstGidIndex(gid)];
            var atlasCoords = new Vector2I(effectiveGid % atlasWidth, effectiveGid / atlasWidth);
            if (!atlasSource.HasTile(atlasCoords))
            {
                atlasSource.CreateTile(atlasCoords);
                var currentTile = atlasSource.GetTileData(atlasCoords, 0);
                var tileSize = atlasSource.TextureRegionSize;
                if (tileSize.X != _mapTileWidth || tileSize.Y != _mapTileHeight)
                {
                    var diffX = tileSize.X - _mapTileWidth;
                    if (diffX % 2 > 0)
                        diffX -= 1;
                    var diffY = tileSize.Y - _mapTileHeight;
                    if (diffY % 2 > 0)
                        diffY += 1;
                    currentTile.TextureOrigin = new Vector2I(-diffX/2, diffY/2);
                }
            }

            var altId = 0;
            if (flippedH || flippedV || flippedD)
            {
                altId = (flippedH ? 1 : 0) + (flippedV ? 2 : 0) + (flippedD ? 4 : 0);
                if (!atlasSource.HasAlternativeTile(atlasCoords, altId))
                {
                    atlasSource.CreateAlternativeTile(atlasCoords, altId);
                    var tileData = atlasSource.GetTileData(atlasCoords, altId);
                    tileData.FlipH = flippedH;
                    tileData.FlipV = flippedV;
                    tileData.Transpose = flippedD;
                    var tileSize = atlasSource.TextureRegionSize;
                    if (flippedD)
                        tileSize = new Vector2I(tileSize.Y, tileSize.X);
                    if (tileSize.X != _mapTileWidth || tileSize.Y != _mapTileHeight)
                    {
                        var diffX = tileSize.X - _mapTileWidth;
                        if (diffX % 2 != 0) 
                            diffX -= 1;
                        var diffY = tileSize.Y - _mapTileHeight;
                        if (diffY % 2 != 0)
                            diffY += 1;
                        tileData.TextureOrigin = new Vector2I(-diffX/2, diffY/2);
                    }
                    CreatePolygonsOnAlternativeTiles(atlasSource.GetTileData(atlasCoords, 0), tileData, altId);
                }
            }
            _tilemap.SetCell(_tmLayerCounter, cellCoords, sourceId, atlasCoords, altId);
        }
    }

    private ObjectClass GetObjectClass(string className)
    {
        var cn = className.ToLower();
        return cn switch
        {
            "" => ObjectClass.Body,
            "collision" => ObjectClass.Body,
            "staticbody" => ObjectClass.Body,
            "area" => ObjectClass.Area,
            "navigation" => ObjectClass.Navigation,
            "occluder" => ObjectClass.Occluder,
            "occlusion" => ObjectClass.Occluder,
            "line" => ObjectClass.Line,
            "path" => ObjectClass.Path,
            "polygon" => ObjectClass.Polygon,
            _ => ObjectClass.Unknown
        };
    }

    private void HandleObject(Dictionary obj, Node layerNode, TileSet tileset, Vector2 offSet)
    {
        var objX = (float)obj.GetValueOrDefault("x", offSet.X);
        var objY = (float)obj.GetValueOrDefault("y", offSet.Y);
        var objRot = (float)obj.GetValueOrDefault("rotation", 0.0f);
        var objWidth = (float)obj.GetValueOrDefault("width", 0.0f);
        var objHeight = (float)obj.GetValueOrDefault("height", 0.0f);
        var objVisible = (bool)obj.GetValueOrDefault("visible", true);
        var objName = (string)obj.GetValueOrDefault("name", "");
        var classString = (string)obj.GetValueOrDefault("class", "");
        var objClass = GetObjectClass(classString);

        if (obj.ContainsKey("template"))
        {
            var templatePath = _basePath.PathJoin((string)obj["template"]);
            var templateDict = DictionaryBuilder.GetDictionary(templatePath);
            //var templateFirstGids = new Array<int>();
            TileSet templateTileSet = null;
            if (templateDict.ContainsKey("tilesets"))
            {
                var tileSets = (Array<Dictionary>)templateDict["tilesets"];
                //foreach (var tileSet in tileSets)
                //    templateFirstGids.Add((int)tileSet["firstgid"]);
                var tilesetCreator = new TilesetCreator();
                tilesetCreator.SetBasePath(templatePath);
                templateTileSet = tilesetCreator.CreateFromDictionaryArray(tileSets);
                _errorCount += tilesetCreator.GetErrorCount();
                _warningCount += tilesetCreator.GetWarningCount();
            }

            if (templateDict.ContainsKey("objects"))
            {
                foreach (var templateObj in (Array<Dictionary>)templateDict["objects"])
                {
                    HandleObject(templateObj, layerNode, templateTileSet, new Vector2(objX, objY));
                }
            }
        }

        if (obj.ContainsKey("gid"))
        {
            // gid refers to a tile in a tile set and object is created as sprite
            var intId = (uint)obj.GetValueOrDefault("gid", 0);
            intId &= 0xFFFFFFFF;
            var flippedH = (intId & FlippedHorizontallyFlag) > 0;
            var flippedV = (intId & FlippedVerticallyFlag) > 0;
            //var flippedD = (intId & FlippedDiagonallyFlag) > 0;
            var gid = (int)(intId & 0x0FFFFFFF);

            var sourceId = GetMatchingSourceId(gid);
            var firstGidId = GetFirstGidIndex(gid);
            if (firstGidId > sourceId)
                sourceId = firstGidId;
            // Should not be the case, but who knows...
            if (sourceId < 0) return;
        
            var gidSource = (TileSetAtlasSource)tileset.GetSource(sourceId);
            var objSprite = new Sprite2D();
            layerNode.AddChild(objSprite);
            objSprite.Owner = _baseNode;
            objSprite.Name = objName != "" ?
                objName : (gidSource.ResourceName != "" ?
                    gidSource.ResourceName : gidSource.Texture.ResourcePath.GetFile().GetBaseName() + "_tile");
            objSprite.Position = TransposeCoords(objX, objY);
            objSprite.Texture = gidSource.Texture;
            if (GetNumTilesForSourceId(sourceId) > 1)
            {
                // Object is tile from partitioned tileset 
                var atlasWidth = gidSource.GetAtlasGridSize().X;

                // Can be zero, if tileset had an error
                if (atlasWidth <= 0) return;
          
                var effectiveGid = gid - _firstGids[GetFirstGidIndex(gid)];
                var atlasCoords = new Vector2I(effectiveGid % atlasWidth, effectiveGid / atlasWidth);
                if (!gidSource.HasTile(atlasCoords))
                    gidSource.CreateTile(atlasCoords);
                objSprite.RegionEnabled = true;
                var regionSize = (Vector2)gidSource.TextureRegionSize;
                var pos = atlasCoords * regionSize;
                if (GetProperty(obj, "clip_artifacts", "bool") == "true")
                {
                    pos += new Vector2(0.5f, 0.5f);   
                    regionSize -= new Vector2(1.0f, 1.0f);
                    objWidth -= 1.0f;
                    objHeight -= 1.0f;
                }
                objSprite.RegionRect = new Rect2(pos, regionSize);
                objSprite.Offset = _mapOrientation switch
                {
                    "orthogonal" => new Vector2(regionSize.X / 2.0f, -regionSize.Y / 2.0f),
                    "isometric" => new Vector2(0.0f, -regionSize.Y / 2.0f),
                    _ => new Vector2(regionSize.X / 2.0f, -regionSize.Y / 2.0f)
                };
                if (Math.Abs(regionSize.X - (int)objWidth) > 0.01f || Math.Abs(regionSize.Y - (int)objHeight) > 0.01f)
                {
                    var scaleX = objWidth / regionSize.X;
                    var scaleY = objHeight / regionSize.Y;
                    objSprite.Scale = new Vector2(scaleX, scaleY);
                }
            }
            else
            {
                // Object is single image tile
                var gidWidth = gidSource.Texture.GetWidth();
                var gidHeight = gidSource.Texture.GetHeight();
                objSprite.Offset = _mapOrientation switch
                {
                    "orthogonal" => new Vector2(gidSource.Texture.GetWidth() / 2.0f, -gidSource.Texture.GetHeight() / 2.0f),
                    "isometric" => new Vector2(0.0f, -gidSource.Texture.GetHeight() / 2.0f),
                    _ => new Vector2(gidSource.Texture.GetWidth() / 2.0f, -gidSource.Texture.GetHeight() / 2.0f)
                };
                if ((gidWidth != (int)objWidth) || (gidHeight != (int)objHeight))
                {
                    var scaleX = objWidth / gidWidth;
                    var scaleY = objHeight / gidHeight;
                    objSprite.Scale = new Vector2(scaleX, scaleY);
                }
            }

            objSprite.FlipH = flippedH;
            objSprite.FlipV = flippedV;
            objSprite.RotationDegrees = objRot;
            objSprite.Visible = objVisible;
            // if (!_useDefaultFilter)
            //     objSprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

            if (obj.ContainsKey("properties"))
                HandleProperties(objSprite, (Array<Dictionary>)obj["properties"]);
        }
        else if (obj.ContainsKey("text"))
        {
            var objText = new Label();
            layerNode.AddChild(objText);
            objText.Owner = _baseNode;
            objText.Name = (objName != "") ? objName : "Text";
            objText.Position = TransposeCoords(objX, objY);
            objText.Size = new Vector2(objWidth, objHeight);
            objText.ClipText = true;
            objText.RotationDegrees = objRot;
            objText.Visible = objVisible;
            var txt = (Dictionary)obj["text"];
            objText.Text = (string)txt.GetValueOrDefault("text", "Hello World");
            var wrap = (bool)txt.GetValueOrDefault("wrap", false);
            objText.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            var alignH = (string)txt.GetValueOrDefault("halign", "left");
            objText.HorizontalAlignment = alignH switch
            {
                "left" => HorizontalAlignment.Left,
                "center" => HorizontalAlignment.Center,
                "right" => HorizontalAlignment.Right,
                "justify" => HorizontalAlignment.Fill,
                _ => HorizontalAlignment.Left
            };
            var alignV = (string)txt.GetValueOrDefault("valign", "top");
            objText.VerticalAlignment = alignV switch
            {
                "top" => VerticalAlignment.Top,
                "center" => VerticalAlignment.Center,
                "bottom" => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };
            var fontFamily = (string)txt.GetValueOrDefault("fontfamily", "Sans-Serif");
            var font = new SystemFont();
            font.FontNames = new[] { fontFamily };
            font.Oversampling = 5.0f;
            objText.AddThemeFontOverride("font", font);
            var fontSize = (int)txt.GetValueOrDefault("pixelsize", 16);
            objText.AddThemeFontSizeOverride("font_size", fontSize);
            var fontColor = (string)txt.GetValueOrDefault("color", "#000000");
            objText.AddThemeColorOverride("font_color", new Color(fontColor));
            if (obj.ContainsKey("properties"))
                HandleProperties(objText, (Array<Dictionary>)obj["properties"]);
        }
        else if (!obj.ContainsKey("template"))
        {
            if (objClass == ObjectClass.Unknown)
            {
                GD.PrintRich($"[color={WarningColor}] -- Unknown class '{classString}'. -> Assuming Default[/color]");
                _warningCount++;
                objClass = ObjectClass.Body;
            }
            var objectBaseCoords = TransposeCoords(objX, objY);
            if (obj.ContainsKey("point"))
            {
                var marker = new Marker2D();
                layerNode.AddChild(marker);
                marker.Owner = _baseNode;
                marker.Name = (objName != "") ? objName : "point";
                marker.Position = objectBaseCoords;
                marker.RotationDegrees = objRot;
                marker.Visible = objVisible;
                if (obj.ContainsKey("properties"))
                    HandleProperties(marker, (Array<Dictionary>)obj["properties"]);
            }
            else if (obj.ContainsKey("polygon"))
            {
                switch (objClass)
                {
                    case ObjectClass.Body or ObjectClass.Area:
                    {
                        CollisionObject2D co;
                        if (objClass == ObjectClass.Area)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var polygonShape = new CollisionPolygon2D();
                        polygonShape.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        co.AddChild(polygonShape);
                        polygonShape.Owner = _baseNode;
                        polygonShape.Name = (objName != "") ? objName : "Polygon Shape";
                        polygonShape.Position = Vector2.Zero;
                        polygonShape.RotationDegrees = objRot;
                        if (obj.ContainsKey("properties"))
                            HandleProperties(co, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Navigation:
                    {
                        var navRegion = new NavigationRegion2D();
                        layerNode.AddChild(navRegion);
                        navRegion.Owner = _baseNode;
                        navRegion.Name = (objName != "") ? objName + " (NR)" : "Navigation";
                        navRegion.Position = objectBaseCoords;
                        navRegion.RotationDegrees = objRot;
                        navRegion.Visible = objVisible;

                        var navPoly = new NavigationPolygon();
                        navRegion.NavigationPolygon = navPoly;
                        navPoly.AddOutline(PolygonFromArray((Array<Dictionary>)obj["polygon"]));
                        navPoly.MakePolygonsFromOutlines();
                        if (obj.ContainsKey("properties"))
                            HandleProperties(navRegion, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Occluder:
                    {
                        var lightOcc = new LightOccluder2D();
                        layerNode.AddChild(lightOcc);
                        lightOcc.Owner = _baseNode;
                        lightOcc.Name = (objName != "") ? objName + " (LO)" : "Occluder";
                        lightOcc.Position = objectBaseCoords;
                        lightOcc.RotationDegrees = objRot;
                        lightOcc.Visible = objVisible;
                        
                        var occPoly = new OccluderPolygon2D();
                        lightOcc.Occluder = occPoly;
                        occPoly.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        if (obj.ContainsKey("properties"))
                            HandleProperties(lightOcc, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Polygon:
                    {
                        var polygon = new Polygon2D();
                        layerNode.AddChild(polygon);
                        polygon.Owner = _baseNode;
                        polygon.Name = (objName != "") ? objName : "Polygon";
                        polygon.Position = objectBaseCoords;
                        polygon.RotationDegrees = objRot;
                        polygon.Visible = objVisible;
                        polygon.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        
                        if (obj.ContainsKey("properties"))
                            HandleProperties(polygon, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                }
            }
            else if (obj.ContainsKey("polyline"))
            {
                switch (objClass)
                {
                    case ObjectClass.Line:
                    {
                        var line = new Line2D();
                        layerNode.AddChild(line);
                        line.Owner = _baseNode;
                        line.Name = (objName != "") ? objName : "Line";
                        line.Position = objectBaseCoords;
                        line.Visible = objVisible;
                        line.RotationDegrees = objRot;
                        line.Width = 1.0f;
                    
                        line.Points = PolygonFromArray((Array<Dictionary>)obj["polyline"]);

                        if (obj.ContainsKey("properties"))
                            HandleProperties(line, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Path:
                    {
                        var path = new Path2D();
                        layerNode.AddChild(path);
                        path.Owner = _baseNode;
                        path.Name = (objName != "") ? objName : "Path";
                        path.Position = objectBaseCoords;
                        path.Visible = objVisible;
                        path.RotationDegrees = objRot;

                        var curve = new Curve2D();
                        foreach (var point in (Array<Dictionary>)obj["polyline"])
                            curve.AddPoint(new Vector2((float)point["x"], (float)point["y"]));
                        path.Curve = curve;

                        if (obj.ContainsKey("properties"))
                            HandleProperties(path, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    default:
                    {
                        CollisionObject2D co;
                        if (objClass == ObjectClass.Body)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var polylinePoints = (Array<Dictionary>)obj["polyline"];
                        for (var i = 0; i < polylinePoints.Count - 1; i++)
                        {
                            var collisionShape = new CollisionShape2D();
                            co.AddChild(collisionShape);
                            var segmentShape = new SegmentShape2D();
                            segmentShape.A =
                                TransposeCoords((float)polylinePoints[i]["x"], (float)polylinePoints[i]["y"], true);
                            segmentShape.B = TransposeCoords((float)polylinePoints[i + 1]["x"],
                                (float)polylinePoints[i + 1]["y"], true);
                            collisionShape.Owner = _baseNode;
                            collisionShape.Shape = segmentShape;
                            collisionShape.Position = new Vector2(objWidth / 2.0f, objHeight / 2.0f);
                            collisionShape.RotationDegrees = objRot;
                            collisionShape.Name = "Segment Shape";
                        }

                        if (obj.ContainsKey("properties"))
                            HandleProperties(co, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                }
            }
            else
            {
                switch (objClass)
                {
                    case ObjectClass.Body or ObjectClass.Area:
                    {
                        CollisionObject2D co;
                        if (objClass == ObjectClass.Area)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var collisionShape = new CollisionShape2D();
                        co.AddChild(collisionShape);
                        collisionShape.Owner = _baseNode;
                        if (obj.ContainsKey("ellipse"))
                        {
                            var capsuleShape = new CapsuleShape2D();
                            capsuleShape.Height = objHeight;
                            capsuleShape.Radius = objWidth / 2.0f;
                            collisionShape.Shape = capsuleShape;
                            collisionShape.Name = (objName != "") ? objName : "Capsule Shape";
                        }
                        else // Rectangle
                        {
                            var rectangleShape = new RectangleShape2D();
                            rectangleShape.Size = new Vector2(objWidth, objHeight);
                            collisionShape.Shape = rectangleShape;
                            collisionShape.Name = (objName != "") ? objName : "Rectangle Shape";
                        }

                        if (_mapOrientation == "isometric")
                        {
                            if (_isoRot == 0.0f)
                            {
                                var q = (float)_mapTileHeight / _mapTileWidth;
                                q *= q;
                                var cosA = Math.Sqrt(1 / (q + 1));
                                _isoRot = (float)(Math.Acos(cosA) * 180 / Math.PI);
                                _isoSkew = (float)((90 - 2 * _isoRot) * Math.PI / 180);
                                var scale = (float)(_mapTileWidth / (_mapTileHeight * 2 * cosA));
                                _isoScale = new Vector2(scale, scale);
                            }

                            collisionShape.Skew = _isoSkew;
                            collisionShape.Scale = _isoScale;
                            objRot += _isoRot;
                        }

                        collisionShape.Position = TransposeCoords(objWidth / 2.0f, objHeight / 2.0f, true);
                        collisionShape.RotationDegrees = objRot;
                        if (obj.ContainsKey("properties"))
                            HandleProperties(co, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Navigation when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for NavigationRegion2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case ObjectClass.Navigation:
                    {
                        var navRegion = new NavigationRegion2D();
                        layerNode.AddChild(navRegion);
                        navRegion.Owner = _baseNode;
                        navRegion.Name = (objName != "") ? objName + " (NR)" : "Navigation";
                        navRegion.Position = objectBaseCoords;
                        navRegion.RotationDegrees = objRot;
                        navRegion.Visible = objVisible;

                        var navPoly = new NavigationPolygon();
                        navRegion.NavigationPolygon = navPoly;
                        navPoly.AddOutline(PolygonFromRectangle(objWidth, objHeight));
                        navPoly.MakePolygonsFromOutlines();
                        if (obj.ContainsKey("properties"))
                            HandleProperties(navRegion, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Occluder when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for LightOccluder2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case ObjectClass.Occluder:
                    {
                        var lightOcc = new LightOccluder2D();
                        layerNode.AddChild(lightOcc);
                        lightOcc.Owner = _baseNode;
                        lightOcc.Name = (objName != "") ? objName + " (LO)" : "Occluder";
                        lightOcc.Position = objectBaseCoords;
                        lightOcc.RotationDegrees = objRot;
                        lightOcc.Visible = objVisible;
                        
                        var occPoly = new OccluderPolygon2D();
                        lightOcc.Occluder = occPoly;
                        occPoly.Polygon = PolygonFromRectangle(objWidth, objHeight);
                        if (obj.ContainsKey("properties"))
                            HandleProperties(lightOcc, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                    case ObjectClass.Polygon when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for Polygon2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case ObjectClass.Polygon:
                    {
                        var polygon = new Polygon2D();
                        layerNode.AddChild(polygon);
                        polygon.Owner = _baseNode;
                        polygon.Name = (objName != "") ? objName : "Polygon";
                        polygon.Position = objectBaseCoords;
                        polygon.RotationDegrees = objRot;
                        polygon.Visible = objVisible;
                        polygon.Polygon = PolygonFromRectangle(objWidth, objHeight);
                        
                        if (obj.ContainsKey("properties"))
                            HandleProperties(polygon, (Array<Dictionary>)obj["properties"]);
                        break;
                    }
                }
            }
        }
    }

    private static string GetProperty(Dictionary obj, string propertyName, string propertyType)
    {
        const string ret = "";
        if (!obj.ContainsKey("properties")) return ret;
        foreach (var property in (Array<Dictionary>)obj["properties"])
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name.ToLower() == propertyName && type == propertyType)
                return val;
        }

        return ret;
    }

    private Vector2[] PolygonFromArray(Array<Dictionary> polyArray)
    {
        var polygon = new Vector2[polyArray.Count];
        var i = 0;
        foreach (var pt in polyArray)
        {
            var pCoord = TransposeCoords((float)pt["x"], (float)pt["y"], true);
            polygon[i] = pCoord;
            i++;
        }
        
        return polygon;
    }
    
    private Vector2[] PolygonFromRectangle(float width, float height)
    {
        var polygon = new Vector2[4];
        polygon[0] = Vector2.Zero;
        polygon[1].X = polygon[0].X;
        polygon[1].Y = polygon[0].Y + height;
        polygon[2].X = polygon[0].X + width;
        polygon[2].Y = polygon[1].Y;
        polygon[3].X = polygon[2].X;
        polygon[3].Y = polygon[0].Y;
        polygon[1] = TransposeCoords(polygon[1].X, polygon[1].Y, true);
        polygon[2] = TransposeCoords(polygon[2].X, polygon[2].Y, true);
        polygon[3] = TransposeCoords(polygon[3].X, polygon[3].Y, true);
        return polygon;
    }

    private Vector2 TransposeCoords(float x, float y, bool noOffsetX = false)
    {
        if (_mapOrientation == "isometric")
        {
            var transX = (x - y) * _mapTileWidth / _mapTileHeight / 2.0f;
            if (!noOffsetX)
                transX += _mapHeight * _mapTileWidth / 2.0f;
            var transY = (x + y) * 0.5f;
            return new Vector2(transX, transY);
        }

        return new Vector2(x, y);
    }
    
    private int GetFirstGidIndex(int gid)
    {
        var index = 0;
        var gidIndex = 0;
        foreach (var firstgid in _firstGids)
        {
            if (gid >= firstgid)
                gidIndex = index;
            index++;
        }

        return gidIndex;
    }
    
    private int GetMatchingSourceId(int gid)
    {
        var limit = 0;
        var prevSourceId = -1;
        if (_atlasSources == null)
            return -1;
        foreach (Dictionary src in _atlasSources)
        {
            var sourceId = (int)src["sourceId"];
            limit += (int)src["numTiles"] + sourceId - prevSourceId - 1;
            if (gid <= limit)
                return sourceId;
            prevSourceId = sourceId;
        }

        return -1;
    }

    private int GetNumTilesForSourceId(int sourceId)
    {
        foreach (Dictionary src in _atlasSources)
        {
            if ((int)src["sourceId"] == sourceId)
                return (int)src["numTiles"];
        }

        return -1;
    }

    private Resource LoadResourceFromFile(string path)
    {
        var origPath = path;
        Resource ret = null;
        // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
        if (!FileAccess.FileExists(path))
            path = _basePath.GetBaseDir().PathJoin(origPath);
        if (!FileAccess.FileExists(path))
            path = _basePath.PathJoin(origPath);
        if (FileAccess.FileExists(path))
            ret = ResourceLoader.Load(path);
        else
        {
            GD.PrintErr($"ERROR: Resource file '{origPath}' not found.");
            _errorCount++;
        }

        return ret;
    }

    private static uint GetBitmaskIntegerFromString(string maskString, int max)
    {
        uint ret = 0;
        var s1Arr = maskString.Split(',', StringSplitOptions.TrimEntries);
        foreach (var s1 in s1Arr)
        {
            if (s1.Contains('-'))
            {
                var s2Arr = s1.Split('-', 2, StringSplitOptions.TrimEntries);
                if (!int.TryParse(s2Arr[0], out var i1) || !int.TryParse(s2Arr[1], out var i2)) continue;
                if (i1 > i2) continue;
                for (var i = i1; i <= i2; i++)
                    if (i <= max)
                        ret += (uint)Math.Pow(2, i - 1);
            }
            else if (int.TryParse(s1, out var i)) 
                if (i <= max) 
                    ret += (uint)Math.Pow(2, i - 1);
        }

        return ret;
    }
    
    private void HandleProperties(Node targetNode, Array<Dictionary> properties)
    {
        var targetNodeClass = targetNode.GetType();
        var hasChildren = false;
        if (targetNodeClass == typeof(StaticBody2D) || targetNodeClass == typeof(Area2D))
            hasChildren = targetNode.GetChildCount() > 0;
        foreach (var property in properties)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name == "") continue;
            if (name.StartsWith("__") && hasChildren)
            {
                var childPropDict = new Dictionary();
                childPropDict.Add("name", name[2..]);
                childPropDict.Add("type", type);
                childPropDict.Add("value", val);
                var childProps = new Array<Dictionary> { childPropDict };
                foreach (var child in targetNode.GetChildren())
                    HandleProperties(child, childProps);
            }

            switch (name.ToLower())
            {
                // CanvasItem properties
                case "modulate" when (type == "string"):
                    ((CanvasItem)targetNode).Modulate = new Color(val);
                    break;
                case "self_modulate" when (type == "string"):
                    ((CanvasItem)targetNode).SelfModulate = new Color(val);
                    break;
                case "show_behind_parent" when (type == "bool"):
                    ((CanvasItem)targetNode).ShowBehindParent = bool.Parse(val);
                    break;
                case "top_level" when (type == "bool"):
                    ((CanvasItem)targetNode).TopLevel = bool.Parse(val);
                    break;
                case "clip_children" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.ClipChildrenMode.Max)
                        ((CanvasItem)targetNode).ClipChildren = (CanvasItem.ClipChildrenMode)int.Parse(val);
                    break;
                case "light_mask" when (type == "string"):
                    ((CanvasItem)targetNode).LightMask = (int)GetBitmaskIntegerFromString(val, 20);
                    break;
                case "visibility_layer" when (type == "string"):
                    ((CanvasItem)targetNode).VisibilityLayer = GetBitmaskIntegerFromString(val, 20);
                    break;
                case "z_index" when (type == "int"):
                    ((CanvasItem)targetNode).ZIndex = int.Parse(val);
                    break;
                case "z_as_relative" when (type == "bool"):
                    ((CanvasItem)targetNode).ZAsRelative = bool.Parse(val);
                    break;
                case "y_sort_enabled" when (type == "bool"):
                    ((CanvasItem)targetNode).YSortEnabled = bool.Parse(val);
                    break;
                case "texture_filter" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.TextureFilterEnum.Max)
                        ((CanvasItem)targetNode).TextureFilter = (CanvasItem.TextureFilterEnum)int.Parse(val);
                    break;
                case "texture_repeat" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.TextureRepeatEnum.Max)
                        ((CanvasItem)targetNode).TextureRepeat = (CanvasItem.TextureRepeatEnum)int.Parse(val);
                    break;
                case "material" when (type == "file"):
                    ((CanvasItem)targetNode).Material = (Material)LoadResourceFromFile(val);
                    break;
                case "use_parent_material" when (type == "bool"):
                    ((CanvasItem)targetNode).UseParentMaterial = bool.Parse(val);
                    break;

                // TileMap properties
                case "cell_quadrant_size" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMap)):
                    ((TileMap)targetNode).CellQuadrantSize = int.Parse(val);
                    break;
                case "collision_animatable" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(TileMap)):
                    ((TileMap)targetNode).CollisionAnimatable = bool.Parse(val);
                    break;
                case "collision_visibility_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMap)):
                    if (int.Parse(val) < 3)
                        ((TileMap)targetNode).CollisionVisibilityMode = (TileMap.VisibilityMode)int.Parse(val);
                    break;
                case "navigation_visibility_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMap)):
                    if (int.Parse(val) < 3)
                        ((TileMap)targetNode).NavigationVisibilityMode = (TileMap.VisibilityMode)int.Parse(val);
                    break;
                
                // CollisionObject2D properties
                case "disable_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    if (int.Parse(val) < 3)
                        ((CollisionObject2D)targetNode).DisableMode = (CollisionObject2D.DisableModeEnum)int.Parse(val);
                    break;
                case "collision_layer" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionLayer = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_mask" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionMask = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_priority" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionPriority = float.Parse(val);
                    break;
                case "input_pickable" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).InputPickable = bool.Parse(val);
                    break;

                // CollisionPolygon2D properties
                case "build_mode" when type == "int" && hasChildren && int.Parse(val) < 2:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).BuildMode = (CollisionPolygon2D.BuildModeEnum)int.Parse(val);
                    break;

                // CollisionPolygon & CollisionShape2D properties
                case "disabled" when type == "bool" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).Disabled = bool.Parse(val);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).Disabled = bool.Parse(val);
                    break;
                case "one_way_collision" when type == "bool" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).OneWayCollision = bool.Parse(val);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).OneWayCollision = bool.Parse(val);
                    break;
                case "one_way_collision_margin" when type is "float" or "int" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).OneWayCollisionMargin = float.Parse(val);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).OneWayCollisionMargin = float.Parse(val);
                    break;

                // CollisionShape2D properties
                case "debug_color" when type == "string" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).DebugColor = new Color(val);
                    break;
                
                // Area2D properties
                case "monitoring" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Monitoring = bool.Parse(val);
                    break;
                case "monitorable" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Monitorable = bool.Parse(val);
                    break;
                case "priority" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Priority = float.Parse(val);
                    break;
                case "gravity_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).GravitySpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "gravity_point" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPoint = bool.Parse(val);
                    break;
                case "gravity_point_center_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointCenter = new Vector2(float.Parse(val), ((Area2D)targetNode).GravityPointCenter.Y);
                    break;
                case "gravity_point_center_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointCenter = new Vector2(((Area2D)targetNode).GravityPointCenter.X, float.Parse(val));
                    break;
                case "gravity_point_unit_distance" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointUnitDistance = float.Parse(val);
                    break;
                case "gravity_direction_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityDirection = new Vector2(float.Parse(val), ((Area2D)targetNode).GravityDirection.Y);
                    break;
                case "gravity_direction_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityDirection = new Vector2(((Area2D)targetNode).GravityDirection.X, float.Parse(val));
                    break;
                case "gravity" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Gravity = float.Parse(val);
                    break;
                case "linear_damp_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).LinearDampSpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "linear_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).LinearDamp = float.Parse(val);
                    break;
                case "angular_damp_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).AngularDampSpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "angular_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).AngularDamp = float.Parse(val);
                    break;
                
                // StaticBody2D properties
                case "physics_material" when (type == "file"):
                    ((StaticBody2D)targetNode).PhysicsMaterialOverride = (PhysicsMaterial)LoadResourceFromFile(val);
                    break;
                case "constant_linear_velocity_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantLinearVelocity = new Vector2(float.Parse(val), ((StaticBody2D)targetNode).ConstantLinearVelocity.Y);
                    break;
                case "constant_linear_velocity_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantLinearVelocity = new Vector2(((StaticBody2D)targetNode).ConstantLinearVelocity.X, float.Parse(val));
                    break;
                case "constant_angular_velocity" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantAngularVelocity = float.Parse(val);
                    break;
                
                // NavigationRegion2D properties
                case "enabled" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).Enabled = bool.Parse(val);
                    break;
                case "navigation_layers" when type == "string" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).NavigationLayers = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "enter_cost" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).EnterCost = float.Parse(val);
                    break;
                case "travel_cost" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).TravelCost = float.Parse(val);
                    break;

                // LightOccluder2D properties
                case "sdf_collision" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(LightOccluder2D)):
                    ((LightOccluder2D)targetNode).SdfCollision = bool.Parse(val);
                    break;
                case "occluder_light_mask" when type == "string" && targetNodeClass.IsAssignableTo(typeof(LightOccluder2D)):
                    ((LightOccluder2D)targetNode).OccluderLightMask = (int)GetBitmaskIntegerFromString(val, 20);
                    break;

                // Polygon2D properties
                case "color" when type == "string" && targetNodeClass.IsAssignableTo(typeof(Polygon2D)):
                    ((Polygon2D)targetNode).Color = new Color(val);
                    break;

                // Line2D properties
                case "width" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Line2D)targetNode).Width = float.Parse(val);
                    break;
                case "default_color" when type == "string" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Line2D)targetNode).DefaultColor = new Color(val);
                    break;
                
                // Marker2D properties
                case "gizmo_extents" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Marker2D)targetNode).GizmoExtents = float.Parse(val);
                    break;
                
                // Other properties are added as Metadata
                default:
                {
                    targetNode.SetMeta(name, val);
                    break;
                }
            }
        }
    }
}
