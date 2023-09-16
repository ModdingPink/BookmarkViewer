using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using HMUI;
using IPA.Config.Data;
using IPA.Utilities;
using Polyglot;
using SongCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BookmarkViewer.Patches
{
    internal class BookmarkPatches
    {
        static float _minX;
        static float _maxX;

        static List<Graphic> _graphicsPool = new List<Graphic>();
        static CurvedTextMeshPro _currentBookmarkText = null;

        static List<Bookmark> _bookmarks = new List<Bookmark>();

        static Bookmark? _currentBookmark = null;

        static Vector3 _bookmarkGraphicScale = Vector3.one;

        static float FindClosestFloat(float target)
        {
            if (_bookmarks.Count < 1) return 0f;
            float closest = _bookmarks[0].timeInSeconds;
            float minDifference = Math.Abs(target - closest);

            for (int i = 0; i < _bookmarks.Count; i++)
            {
                float f = _bookmarks[i].timeInSeconds;
                float difference = Math.Abs(target - f);
                if (difference < minDifference)
                {
                    minDifference = difference;
                    closest = f;
                }
            }

            return closest;
        }

        public class Bookmark
        {
            public string name;
            public float timeInSeconds;
            public Color color;
            public Graphic graphic;
        }

        static void GetCurrentBookmark(float value)
        {
            Bookmark? bookmark = _bookmarks.FindLast(b => b.timeInSeconds <= value);
            if (_currentBookmark != null){
                if(_currentBookmark.graphic != null){
                    _currentBookmark.graphic.color = _currentBookmark.color.ColorWithAlpha(0.7f);
                    _currentBookmark.graphic.transform.localScale = _bookmarkGraphicScale;
                }
            }
            _currentBookmark = bookmark;
            if (bookmark != null) {
                if (bookmark.graphic != null) {
                    bookmark.graphic.color = bookmark.color.ColorWithAlpha(0.9f);
                    bookmark.graphic.transform.localScale = Vector3.Scale(_bookmarkGraphicScale, new Vector3(1, 1.15f, 1));
                }
            }
        }

        static void UpdateTextValue()
        {
            _currentBookmarkText.text = _currentBookmark != null ? _currentBookmark.name : "";
        }

        [HarmonyPatch(typeof(PracticeViewController))]
        [HarmonyPatch("HandleSongStartSliderValueDidChange")]
        internal class PracticeViewControllerHandleSongStartSliderValueDidChangePatch
        {
            static void Prefix(PracticeViewController __instance, RangeValuesTextSlider slider, ref float value, ref IBeatmapLevel ____level, ref BeatmapDifficulty ____beatmapDifficulty, ref BeatmapCharacteristicSO ____beatmapCharacteristic) {
                if (!Config.Instance.Enabled) return;

                float bookmarkValue = Config.Instance.SnapToBookmark ? FindClosestFloat(value) : value;

                if (Config.Instance.SnapToBookmark && Math.Abs(value - bookmarkValue) < ____level.songDuration / 100)
                {
                    value = bookmarkValue;
                    slider.value = bookmarkValue;
                }

                GetCurrentBookmark(value);
                UpdateTextValue();
            }
        }


        [HarmonyPatch(typeof(PracticeViewController))]
        [HarmonyPatch("DidActivate")]
        internal class PracticeViewControllerActivatePatch
        {
            static void Postfix(PracticeViewController __instance, ref IBeatmapLevel ____level, ref BeatmapDifficulty ____beatmapDifficulty, ref BeatmapCharacteristicSO ____beatmapCharacteristic)
            {
                foreach (var graphic in _graphicsPool)
                {
                    if (graphic == null)
                    {
                        _graphicsPool.Clear();
                        break;
                    }
                    graphic.gameObject.SetActive(false);
                }

                if (!Config.Instance.Enabled) return;

                if (!(____level is CustomBeatmapLevel)) {
                    _bookmarks.Clear();
                    if(_currentBookmarkText != null) _currentBookmarkText.text = "";
                    return;
                } 

                TimeSlider slider = __instance.GetField<TimeSlider, PracticeViewController>("_songStartSlider");
                Graphic handleGraphic = slider.GetField<Graphic, TextSlider>("_handleGraphic");

                SetupNameText(slider);
                GetSliderMinMax(slider, handleGraphic);

                ObtainBookmarksFromLevel(____level, ____beatmapCharacteristic, ____beatmapDifficulty);

                SetBookmarkVisuals(handleGraphic, ____level);
                GetCurrentBookmark(slider.value - ____level.songDuration / 100);
                UpdateTextValue();
            }

            static void ObtainBookmarksFromLevel(IBeatmapLevel level, BeatmapCharacteristicSO beatmapCharacteristic, BeatmapDifficulty difficulty)
            {
                _bookmarks.Clear();
                var diffBeatmap = level.beatmapLevelData.GetDifficultyBeatmapSet(beatmapCharacteristic.serializedName).difficultyBeatmaps.Where(bm => bm.difficulty == difficulty).FirstOrDefault();
                if (diffBeatmap != null)
                {
                    var mapData = diffBeatmap.GetBeatmapSaveData();

                    if (mapData != null)
                    {
                        var bookmarksUseBpmEvents = mapData.version2_6_0AndEarlier
                            ? mapData.customData.Get<bool?>("_bookmarksUseOfficialBpmEvents")
                            : mapData.customData.Get<bool?>("bookmarksUseOfficialBpmEvents");

                        var bookmarkList = mapData.customData.Get<List<object>>(mapData.version2_6_0AndEarlier ? "_bookmarks" : "bookmarks")?.Cast<CustomData>();
                        if (bookmarkList != null)
                        {
                            foreach (var bookmarkItem in bookmarkList)
                            { 
                                var bookmark = new Bookmark();

                                var bookmarkBeat = bookmarkItem.Get<float>(mapData.version2_6_0AndEarlier ? "_time" : "b");
                                bookmark.timeInSeconds = (bookmarksUseBpmEvents == true)
                                    ? BeatsToSecondsWithBpmEvents(level.beatsPerMinute, bookmarkBeat, mapData.bpmEvents)
                                    : BeatsToSeconds(level.beatsPerMinute, bookmarkBeat);
                                bookmark.name = bookmarkItem.Get<string>(mapData.version2_6_0AndEarlier ? "_name" : "n");

                                List<object>? colorArray = bookmarkItem.Get<List<object>>(mapData.version2_6_0AndEarlier ? "_color" : "c");
                                if(colorArray == null)
                                {
                                    bookmark.color = Color.red;
                                }
                                else
                                {
                                    IEnumerable<float> colorFloats = colorArray.Select(n => Convert.ToSingle(n));
                                    bookmark.color = new Color(colorFloats.ElementAt(0), colorFloats.ElementAt(1), colorFloats.ElementAt(2));
                                }

                                _bookmarks.Add(bookmark);
                            }
                        }
                    }
                }
                _bookmarks.OrderBy(b => b.timeInSeconds);
            }

            static void SetBookmarkVisuals(Graphic handleGraphic, IBeatmapLevel level)
            {
                int item = 0;
                foreach(Bookmark bookmarkItem in _bookmarks)
                {
                    Graphic bookmark;

                    if (_graphicsPool.Count > item)
                    {
                        bookmark = _graphicsPool[item];
                        bookmark.gameObject.SetActive(true);
                    }
                    else
                    {
                        bookmark = GameObject.Instantiate(handleGraphic, handleGraphic.transform.parent);
                        _graphicsPool.Add(bookmark);
                    }
                    bookmarkItem.graphic = bookmark;
                    bookmark.transform.localScale = _bookmarkGraphicScale;
                    bookmark.transform.position = new Vector3(GetBookmarkXPosition(bookmarkItem.timeInSeconds, level), handleGraphic.transform.position.y, handleGraphic.transform.position.z);
                    bookmark.color = bookmarkItem.color.ColorWithAlpha(0.75f);
                    item++;
                }
            }

            static void SetupNameText(TimeSlider slider)
            {
                if (_currentBookmarkText != null) return;

                var currentBookmarkTextGO = GameObject.Instantiate(slider.transform.parent.Find("SongStartLabel").gameObject, slider.transform.parent);
                GameObject.Destroy(currentBookmarkTextGO.GetComponent<LocalizedTextMeshProUGUI>());
                _currentBookmarkText = currentBookmarkTextGO.GetComponent<CurvedTextMeshPro>();
                _currentBookmarkText.transform.position = new Vector3(-0.025f, 2.04f, 4.35f);
                _currentBookmarkText.alignment = TMPro.TextAlignmentOptions.Right;
                _currentBookmarkText.text = "";
            }



            static void GetSliderMinMax(TimeSlider slider, Graphic handleGraphic)
            {
                var value = slider.value;
                slider.value = slider.maxValue;
                _maxX = handleGraphic.transform.position.x + (Math.Abs(1 - Config.Instance.BookmarkWidthSize) * 0.05f);
                slider.value = slider.minValue;
                _minX = handleGraphic.transform.position.x;
                slider.value = value;
                _bookmarkGraphicScale = Vector3.Scale(handleGraphic.transform.localScale, new Vector3(Config.Instance.BookmarkWidthSize, 1, 1));
            }


            public static float BeatsToSeconds(float bpm, float beat)
            {
                return (60.0f / bpm) * beat;
            }

            private static float BeatsToSecondsWithBpmEvents(float levelBpm, float beat, IList<BeatmapSaveDataVersion3.BeatmapSaveData.BpmChangeEventData> bpmEvents)
            {
                // Start with level bpm if the bpm at beat 0 is missing for some reason
                var defaultBpmEvent = new BeatmapSaveDataVersion3.BeatmapSaveData.BpmChangeEventData(0, levelBpm);
                var bpmEventsBeforeBookmark = new List<BeatmapSaveDataVersion3.BeatmapSaveData.BpmChangeEventData> { defaultBpmEvent };
                bpmEventsBeforeBookmark.AddRange(bpmEvents.Where(x => x.beat <= beat));

                var timeInSeconds = 0f;
                for (var i = 0; i < bpmEventsBeforeBookmark.Count - 1; i++)
                {
                    var bpmEvent = bpmEventsBeforeBookmark[i];
                    var nextBpmEvent = bpmEventsBeforeBookmark[i + 1];

                    var timeDiff = nextBpmEvent.beat - bpmEvent.beat;
                    timeInSeconds += timeDiff * (60f / bpmEvent.bpm);
                }
                timeInSeconds += (beat - bpmEventsBeforeBookmark.Last().beat) * (60f / bpmEventsBeforeBookmark.Last().bpm);
                return timeInSeconds;
            }

            static float GetBookmarkXPosition(float time, IBeatmapLevel level)
            {
                return Mathf.Lerp(_minX, _maxX, Mathf.InverseLerp(0, level.songDuration, time));
            }

        }
    }
}
