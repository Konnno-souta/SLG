
using System.Collections;
using UnityEngine;

/// <summary>
/// 第26段階：FE風戦闘カメラ。
///
/// 通常：元の上俯瞰カメラ
/// 攻撃：攻撃者の斜め後方から2人を映す
/// 終了：元の位置と画角に戻る
///
/// ダメージ・HP・ターンは変更しない。
/// </summary>
public sealed class BattleCombatCameraController
    : MonoBehaviour
{
    [Header("カメラ")]
    [SerializeField]
    private Camera targetCamera;

    [Tooltip("通常カメラを毎フレーム動かすスクリプトがあれば登録")]
    [SerializeField]
    private Behaviour[] temporarilyDisabledScripts;

    [Header("戦闘時のカメラ位置")]
    [SerializeField, Min(0.1f)]
    private float backDistance = 3.5f;

    [SerializeField]
    private float sideDistance = 1.3f;

    [SerializeField, Min(0.1f)]
    private float cameraHeight = 2.8f;

    [SerializeField]
    private float focusHeight = 1.0f;

    [Header("戦闘時の画角")]
    [SerializeField]
    private bool usePerspectiveForCombat = true;

    [SerializeField, Range(15f, 90f)]
    private float combatFieldOfView = 48f;

    [SerializeField, Min(0.5f)]
    private float combatOrthographicSize = 2.8f;

    [Header("切り替え時間")]
    [SerializeField, Min(0f)]
    private float enterDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float exitDuration = 0.4f;

    [Header("被弾後のカメラ保持")]
    [SerializeField, Min(0f)]
    private float holdAfterHit = 0.45f;

    // ====================================
    // 元のカメラ状態
    // ====================================

    private Vector3 savedPosition;
    private Quaternion savedRotation;

    private float savedFieldOfView;
    private float savedOrthographicSize;
    private bool savedOrthographic;

    private bool hasSavedState;

    private bool[] previousScriptStates;

    public bool IsInCombat => hasSavedState;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogError(
                "戦闘カメラ：Cameraが設定されていません。",
                this
            );
        }
    }

    // ====================================
    // 戦闘カメラへ移行
    // ====================================

    public IEnumerator EnterCombat(
        UnitView attacker,
        UnitView target)
    {
        if (targetCamera == null ||
            attacker == null ||
            target == null ||
            hasSavedState)
        {
            yield break;
        }

        // 通常カメラの状態を保存
        SaveCameraState();

        // 通常カメラの操作スクリプトを停止
        SuspendCameraScripts();

        hasSavedState = true;

        Vector3 attackerPosition =
            attacker.transform.position;

        Vector3 targetPosition =
            target.transform.position;

        // 攻撃者から対象への水平方向
        Vector3 forward =
            targetPosition - attackerPosition;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = attacker.transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                forward
            ).normalized;

        // 2人の中間を注視
        Vector3 focusPoint =
            (attackerPosition + targetPosition) * 0.5f
            + Vector3.up * focusHeight;

        // 攻撃者の斜め後方
        Vector3 desiredPosition =
            focusPoint
            - forward * backDistance
            + right * sideDistance
            + Vector3.up * cameraHeight;

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                focusPoint - desiredPosition,
                Vector3.up
            );

        // 戦闘時の投影方式
        if (usePerspectiveForCombat)
        {
            targetCamera.orthographic = false;
        }
        else
        {
            targetCamera.orthographic = savedOrthographic;

            if (targetCamera.orthographic)
            {
                targetCamera.orthographicSize =
                    combatOrthographicSize;
            }
        }

        yield return AnimateCamera(
            desiredPosition,
            desiredRotation,
            combatFieldOfView,
            enterDuration
        );
    }

    // ====================================
    // 通常カメラへ復帰
    // ====================================

    public IEnumerator ExitCombat(
        bool waitAfterHit = true)
    {
        if (targetCamera == null ||
            !hasSavedState)
        {
            yield break;
        }

        // 被弾演出を見せる時間
        if (waitAfterHit && holdAfterHit > 0f)
        {
            yield return new WaitForSecondsRealtime(
                holdAfterHit
            );
        }

        yield return AnimateCamera(
            savedPosition,
            savedRotation,
            savedFieldOfView,
            exitDuration
        );

        targetCamera.orthographic =
            savedOrthographic;

        targetCamera.orthographicSize =
            savedOrthographicSize;

        targetCamera.fieldOfView =
            savedFieldOfView;

        RestoreCameraScripts();

        hasSavedState = false;
    }

    // ====================================
    // カメラの滑らかな移動
    // ====================================

    private IEnumerator AnimateCamera(
        Vector3 destination,
        Quaternion destinationRotation,
        float destinationFov,
        float duration)
    {
        Transform cameraTransform =
            targetCamera.transform;

        Vector3 startPosition =
            cameraTransform.position;

        Quaternion startRotation =
            cameraTransform.rotation;

        float startFov =
            targetCamera.fieldOfView;

        float elapsed = 0f;

        if (duration > 0f)
        {
            while (elapsed < duration)
            {
                // ヒットストップの影響を受けずに動く
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(
                    elapsed / duration
                );

                // 加減速を滑らかにする
                t = Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

                cameraTransform.position =
                    Vector3.Lerp(
                        startPosition,
                        destination,
                        t
                    );

                cameraTransform.rotation =
                    Quaternion.Slerp(
                        startRotation,
                        destinationRotation,
                        t
                    );

                if (!targetCamera.orthographic)
                {
                    targetCamera.fieldOfView =
                        Mathf.Lerp(
                            startFov,
                            destinationFov,
                            t
                        );
                }

                yield return null;
            }
        }

        cameraTransform.SetPositionAndRotation(
            destination,
            destinationRotation
        );

        if (!targetCamera.orthographic)
        {
            targetCamera.fieldOfView =
                destinationFov;
        }
    }

    // ====================================
    // 元の状態を保存
    // ====================================

    private void SaveCameraState()
    {
        Transform cameraTransform =
            targetCamera.transform;

        savedPosition =
            cameraTransform.position;

        savedRotation =
            cameraTransform.rotation;

        savedFieldOfView =
            targetCamera.fieldOfView;

        savedOrthographicSize =
            targetCamera.orthographicSize;

        savedOrthographic =
            targetCamera.orthographic;
    }

    // ====================================
    // 通常カメラ制御を一時停止
    // ====================================

    private void SuspendCameraScripts()
    {
        if (temporarilyDisabledScripts == null)
        {
            return;
        }

        previousScriptStates =
            new bool[temporarilyDisabledScripts.Length];

        for (int i = 0;
             i < temporarilyDisabledScripts.Length;
             i++)
        {
            Behaviour script =
                temporarilyDisabledScripts[i];

            if (script == null ||
                script == this ||
                script == targetCamera)
            {
                continue;
            }

            previousScriptStates[i] =
                script.enabled;

            script.enabled = false;
        }
    }

    private void RestoreCameraScripts()
    {
        if (temporarilyDisabledScripts == null ||
            previousScriptStates == null)
        {
            return;
        }

        int count = Mathf.Min(
            temporarilyDisabledScripts.Length,
            previousScriptStates.Length
        );

        for (int i = 0; i < count; i++)
        {
            Behaviour script =
                temporarilyDisabledScripts[i];

            if (script == null ||
                script == this ||
                script == targetCamera)
            {
                continue;
            }

            script.enabled =
                previousScriptStates[i];
        }

        previousScriptStates = null;
    }

    // ====================================
    // 中断された場合の復帰
    // ====================================

    private void OnDisable()
    {
        if (!hasSavedState)
        {
            return;
        }

        if (targetCamera != null)
        {
            targetCamera.transform
                .SetPositionAndRotation(
                    savedPosition,
                    savedRotation
                );

            targetCamera.orthographic =
                savedOrthographic;

            targetCamera.orthographicSize =
                savedOrthographicSize;

            targetCamera.fieldOfView =
                savedFieldOfView;
        }

        RestoreCameraScripts();

        hasSavedState = false;
    }
}
