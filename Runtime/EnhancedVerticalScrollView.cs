using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnhancedVerticalScrollView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int totalCount = -1;

    public int itemCount = 5;

    [Tooltip("滚动速度")]
    public float speed = 0.006f;       // 滚动速度

    [Tooltip("插值动画的最大时间")]
    public float lerpDuration = 1.0f;   // 插值动画的最大时间

    public int currentCenterIndex { get; private set; } = 0;

    public RectTransform content;

    public Func<Transform, GameObject> onGetObject;

    public Action<GameObject> onReturnObject;

    public Action<GameObject, int> onProvideData;

    public Action<GameObject, bool> onSelected;

    // item的x轴位置曲线
    public AnimationCurve xPositionCurve = AnimationCurve.Constant(0, 1, 0);

    // item的y轴位置曲线
    public AnimationCurve yPositionCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0)) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };

    // item的缩放曲线
    public AnimationCurve scaleCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)) { preWrapMode = WrapMode.PingPong, postWrapMode = WrapMode.PingPong };

    // item的深度曲线, 注意:曲线顶峰需要和中心item对齐
    public AnimationCurve depthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));

    private RectTransform m_Transform;

    private float mCurrentDuration;     // tween已经过的时间

    private bool enableLerpTween = false;
    private float mOriginValue;
    private float mTargetValue;
    private float mCurrentValue = 0f;
    private float mMinValue = 0f;
    private float mMaxValue = 0f;

    private int mCenterIndex = 0;
    private float dFactor = 0.2f;

    private EnhancedItem[] items;

    private EnhancedItem preCenterItem;
    private EnhancedItem curCenterItem;

    void Awake() {
        m_Transform = content ?? transform as RectTransform;
    }

    // Update is called once per frame
    void Update()
    {
        if (enableLerpTween)
        {
            TweenViewToTarget();
        }
    }

    public void RefillCells()
    {
        preCenterItem = null;
        curCenterItem = null;

        if (items != null)
        {
            for (int i = items.Length - 1; i >= 0; i--)
            {
                onReturnObject(items[i].GameObject);
            }
            items = null;
        }

        int count = Mathf.Max(5, itemCount);
        items = new EnhancedItem[count];

        dFactor = 1f / count;

        mCenterIndex = count / 2;

        if (totalCount >= 0)
        {
            mMinValue = 0f;
            mMaxValue = Mathf.Max(0, totalCount - 1) * dFactor;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = onGetObject(m_Transform);
            items[i] = new EnhancedItem() {
                GameObject = go,
                CenterOffset = dFactor * (i + 0.5f),
            };
        }

        LerpTweenToTarget(0.0f, 0.0f, false);
    }

    private void TweenViewToTarget() {
        //var _speed = speed * 10f;

        //var value = 0f;

        //if ((mTargetValue - mCurrentValue) > 0)
        //{
        //    value = mCurrentValue + _speed;
        //    value = Mathf.Min(value, mTargetValue);
        //}
        //else
        //{
        //    value = mCurrentValue - _speed;
        //    value = Mathf.Max(value, mTargetValue);
        //}

        //UpdateView(value);

        //Debug.Log($"ysf:  aaa  {value} {mTargetValue} {value - mTargetValue}");
        //if (value == mTargetValue)
        //{
        //    enableLerpTween = false;
        //    OnTweenOver();
        //}

        var duration = Mathf.Min(mCurrentDuration + Time.deltaTime, lerpDuration);
        mCurrentDuration = duration;
        float percent = duration / lerpDuration;
        float value = Mathf.Lerp(mOriginValue, mTargetValue, percent);
        UpdateView(value);

        if (mCurrentDuration >= lerpDuration)
        {
            enableLerpTween = false;
            OnTweenOver();
        }
    }

    private void LerpTweenToTarget(float originValue, float targetValue, bool needTween)
    {
        if (needTween)
        {
            mOriginValue = originValue;
            mTargetValue = targetValue;
            mCurrentDuration = 0.0f;
        }
        else
        {
            mOriginValue = mTargetValue;
            UpdateView(targetValue);
        }
        enableLerpTween = needTween;
    }

    private void UpdateView(float fValue) {
        var rect = m_Transform.rect;
        var width = rect.width;
        var height = rect.height;

        mCurrentValue = fValue;

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var tran = item.GameObject.transform;
            var time = (item.CenterOffset - fValue);

            var dataIndex = Mathf.RoundToInt(((time - Mathf.FloorToInt(time)) + fValue - 0.5f) / dFactor);

            float yValue = (yPositionCurve.Evaluate(time) - 0.5f) * height;
            float scaleValue = scaleCurve.Evaluate(time);
            float depthValue = depthCurve.Evaluate(time);

            float xValue = width * 0.5f - LayoutUtility.GetPreferredWidth(tran as RectTransform) * scaleValue * 0.5f;

            tran.localPosition = new Vector3(xValue, yValue, 0);
            tran.localScale = new Vector3(scaleValue, scaleValue, 1);
            tran.SetSiblingIndex((int)(depthValue / dFactor));

            item.DataIndex = dataIndex;
            if (totalCount < 0 || (dataIndex >= 0 && dataIndex < totalCount))
            {
                onProvideData(item.GameObject, dataIndex);
            }
            else
            {
                tran.localScale = Vector3.zero;
            }

            var w = LayoutUtility.GetPreferredWidth(tran as RectTransform);
        }

        int closestIndex = GetClosestIndex(out var _);

        preCenterItem = curCenterItem;
        curCenterItem = items[closestIndex];
        currentCenterIndex = curCenterItem.DataIndex;

        if (preCenterItem != null)
            onSelected(preCenterItem.GameObject, false);
        if (curCenterItem != null && (totalCount < 0 || (curCenterItem.DataIndex >= 0 && curCenterItem.DataIndex < totalCount)))
            onSelected(curCenterItem.GameObject, true);
    }

    private void OnTweenOver()
    {
        int closestIndex = GetClosestIndex(out var offset);

        if (Mathf.Abs(offset) < 1e-6) return;

        mOriginValue = mCurrentValue;
        float target = mCurrentValue + offset;

        LerpTweenToTarget(mOriginValue, target, true);

        preCenterItem = curCenterItem;
        curCenterItem = items[closestIndex];
        currentCenterIndex = curCenterItem.DataIndex;

        if (preCenterItem != null)
            onSelected(preCenterItem.GameObject, false);
        if (curCenterItem != null)
            onSelected(curCenterItem.GameObject, true);
    }

    public void ScrollToTarget(GameObject go)
    {
        if (curCenterItem.GameObject == go) return;

        preCenterItem = curCenterItem;
        curCenterItem = items.First(item => item.GameObject == go);

        var offset = GetToCenterOffset(curCenterItem);

        LerpTweenToTarget(mCurrentValue, mCurrentValue + offset, true);
    }

    public void ScrollToCell(int index, bool needTween)
    {
        if (totalCount >= 0)
        {
            if (index < 0)
            {
                Debug.LogError("Out of Index");
                index = 0;
            }
            else if (index >= totalCount)
            {
                Debug.LogError("Out of Index");
                index = totalCount - 1;
            }
        }

        LerpTweenToTarget(mCurrentValue, mCurrentValue + (index - currentCenterIndex) * dFactor, needTween);
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        var delta = eventData.delta;
        if (Mathf.Abs(delta.y) > 0.0f)
        {
            mCurrentValue += delta.y * speed;

            if (totalCount >= 0)
            {
                mCurrentValue = Mathf.Clamp(mCurrentValue, mMinValue - dFactor, mMaxValue + dFactor);
            }
            LerpTweenToTarget(0.0f, mCurrentValue, false);
        }
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (totalCount > 0)
        {
            var fValue = Mathf.Clamp(mCurrentValue, mMinValue, mMaxValue);
            if (Mathf.Abs(fValue - mCurrentValue) > 1.0e-6)
            {
                LerpTweenToTarget(mCurrentValue, fValue, true);
            }
            else
            {
                OnTweenOver();
            }
        }
        else
        {
            OnTweenOver();
        }
    }

    private int GetClosestIndex(out float offset)
    {
        int closestIndex = 0;
        float min = float.MaxValue;

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var dis = GetToCenterOffset(item);

            if (Mathf.Abs(dis) < Mathf.Abs(min))
            {
                closestIndex = i;
                min = dis;
            }
        }

        offset = min;
        return closestIndex;
    }

    private float GetToCenterOffset(EnhancedItem item)
    {
        var o = mCurrentValue - item.CenterOffset;
        var value = o - (int)o;
        var tmp = value + (value < 0 ? 1 : 0);
        var dis = 0.5f - tmp;
        return dis;
    }

    private class EnhancedItem
    {
        public GameObject GameObject;

        public float CenterOffset;

        public int DataIndex;
    }
}
