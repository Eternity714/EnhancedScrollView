using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnhancedVerticalScrollView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField]
    private RectTransform content;
    private RectTransform m_Transform;

    public int totalCount = -1;

    public int itemCount = 5;

    public int currentCenterIndex { get; private set; } = 0;

    [Tooltip("滚动速度")]
    public float speed = 0.006f;       // 滚动速度

    [Tooltip("插值动画的最大时间")]
    public float lerpDuration = 1.0f;   // 插值动画的最大时间

    public Func<Transform, GameObject> onGetObject;

    public Action<GameObject> onReturnObject;

    public Action<GameObject, int> onProvideData;

    public Action<GameObject> onSelected;

    public Action<PointerEventData> onBeginDrag;

    public Action<PointerEventData> onEndDrag;

    public Action<PointerEventData> onDrag;

    public Action onTweenOver;

    // item的y轴位置曲线
    public AnimationCurve yPositionCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0)) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };

    // item的缩放曲线
    public AnimationCurve scaleCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)) { preWrapMode = WrapMode.PingPong, postWrapMode = WrapMode.PingPong };

    // item的深度曲线, 注意:曲线顶峰需要和中心item对齐
    public AnimationCurve depthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));

    private bool enableLerpTween = false;
    private float mCurrentDuration;     // tween已经过的时间
    private float mOriginValue;
    private float mTargetValue;
    private float mCurrentValue = 0f;
    private float mMinValue = 0f;
    private float mMaxValue = 0f;

    private float mCenterValue = 0.5f;

    private float dFactor = 0.2f;

    private EnhancedItem[] items;


    // Start is called before the first frame update
    void Awake()
    {
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

    void OnDestroy()
    {
        ClearCells();
    }

    void ClearCells()
    {
        if (items == null) return;
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i].GameObject == null) continue;
            onReturnObject(items[i].GameObject);
        }
        items = null;
    }

    public void RefillCells()
    {
        ClearCells();

        int count = Mathf.Max(5, itemCount);
        items = new EnhancedItem[count];
        dFactor = 1f / count;

        if (totalCount >= 0)
        {
            mMinValue = 0f;
            mMaxValue = Mathf.Max(0, totalCount - 1) * dFactor;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = onGetObject(m_Transform);
            items[i] = new EnhancedItem()
            {
                GameObject = go,
                CenterOffset = dFactor * (i + 0.5f),
            };
        }

        LerpTweenToTarget(0.0f, 0.0f, false);
        OnTweenOver();
    }

    public void ScrollToCell(int index, bool needTween)
    {
        if (index == currentCenterIndex) return;

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

        if (!needTween)
        {
            OnTweenOver();
        }
    }

    private void LerpTweenToTarget(float originValue, float targetValue, bool needTween)
    {
        if (needTween)
        {
            mOriginValue = originValue;
            mTargetValue = targetValue;
            mCurrentDuration = 0f;
        }
        else
        {
            mOriginValue = mTargetValue;
            UpdateView(targetValue);
        }

        enableLerpTween = needTween;
    }

    private void TweenViewToTarget()
    {
        mCurrentDuration += Time.deltaTime;
        float percent = mCurrentDuration / lerpDuration;
        float value = Mathf.Lerp(mOriginValue, mTargetValue, Mathf.Min(1f, percent));
        UpdateView(value);

        if (mCurrentDuration >= lerpDuration)
        {
            enableLerpTween = false;
            mCurrentDuration = 0f;
            OnTweenOver();
        }
    }

    private void OnTweenOver()
    {
        onTweenOver?.Invoke();
    }

    private void UpdateView(float fValue)
    {
        var rect = content.rect;
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

            float xValue = 0;

            tran.localPosition = new Vector3(xValue, yValue, 0);
            tran.localScale = new Vector3(scaleValue, scaleValue, 1);
            tran.SetSiblingIndex((int)(depthValue / dFactor));

            item.DataIndex = dataIndex;


            if (totalCount >= 0 && (dataIndex < 0 || dataIndex >= totalCount))
            {
                tran.localScale = Vector3.zero;
            }
        }

        int closestIndex = GetClosestIndex(out var _);
        var centerItem = items[closestIndex];
        if (centerItem.DataIndex != currentCenterIndex)
        {
            currentCenterIndex = centerItem.DataIndex;
            onSelected(centerItem.GameObject);
        }

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            onProvideData(item.GameObject, item.DataIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // todo
        onBeginDrag?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var delta = eventData.delta;
        if (Mathf.Abs(delta.y) > 1.0e-6)
        {
            var fValue = mCurrentValue + delta.y * speed;
            if (totalCount > 0)
            {
                fValue = Mathf.Clamp(fValue, mMinValue - dFactor, mMaxValue + dFactor);
            }
            LerpTweenToTarget(0.0f, fValue, false);
        }

        onDrag?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        onEndDrag?.Invoke(eventData);
        var closest = GetClosestIndex(out float offset);
        var fValue = mCurrentValue + offset;
        if (totalCount >= 0)
        {
            fValue = Mathf.Clamp(fValue, mMinValue, mMaxValue);
        }

        if (Mathf.Abs(fValue - mCurrentValue) > 1.0e-6)
        {
            LerpTweenToTarget(mCurrentValue, fValue, true);
        }
        else
        {
            OnTweenOver();
        }
    }

    private int GetClosestIndex(out float offset)
    {
        int closestIndex = 0;
        offset = float.MaxValue;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var dis = GetToCenterOffset(item);
            if (Mathf.Abs(dis) < Mathf.Abs(offset))
            {
                closestIndex = i;
                offset = dis;
            }
        }

        return closestIndex;
    }

    private float GetToCenterOffset(EnhancedItem item)
    {
        var o = mCurrentValue - item.CenterOffset;
        var value = o - (int)o;
        var tmp = value + (value < 0 ? 1 : 0);
        var dis = mCenterValue - tmp;
        return dis;
    }

    private class EnhancedItem
    {
        public GameObject GameObject;

        public float CenterOffset;

        public int DataIndex;
    }
}
