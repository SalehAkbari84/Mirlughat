using UnityEngine;

public class LoadingController : MonoBehaviour
{
    [Header("References")]
    public Animator loadingAnimator1;
    public Animator loadingAnimator2;
    public GameObject mainMenuContent;
    public GameObject levelContent;
    public GameObject mainMenuBackground;
    public GameObject levelBackground;

    [Header("Settings - اسم انیمیشنی که پنل ها را به وسط می آورد")]
    public string comeToCenterAnim1 = "Loading_Close";
    public string comeToCenterAnim2 = "Loading_Close2";

    [Header("Settings - اسم انیمیشنی که پنل ها را به کنار می برد")]
    public string goToSideAnim1 = "Loading_Open";
    public string goToSideAnim2 = "Loading_Open2";

    public float delayBeforeHideLoading = 0.5f;

    public void StartLoading()
    {
        gameObject.SetActive(true);

        float clipLength = 0f;

        if (loadingAnimator1 != null)
        {
            loadingAnimator1.Play(comeToCenterAnim1, 0, 0f);
            clipLength = GetClipLength(loadingAnimator1, comeToCenterAnim1);
        }

        if (loadingAnimator2 != null)
        {
            loadingAnimator2.Play(comeToCenterAnim2, 0, 0f);
        }

        if (clipLength <= 0f)
            clipLength = 1f; // مقدار پیش‌فرض اگر پیدا نشد

        CancelInvoke();
        Invoke(nameof(SwitchContent), clipLength);
    }

    float GetClipLength(Animator animator, string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        return 0f;
    }

    void SwitchContent()
    {
        if (mainMenuContent != null)
            mainMenuContent.SetActive(false);

        if (levelContent != null)
            levelContent.SetActive(true);

        if (mainMenuBackground != null)
            mainMenuBackground.SetActive(false);

        if (levelBackground != null)
            levelBackground.SetActive(true);

        if (loadingAnimator1 != null)
            loadingAnimator1.Play(goToSideAnim1, 0, 0f);

        if (loadingAnimator2 != null)
            loadingAnimator2.Play(goToSideAnim2, 0, 0f);

        Invoke(nameof(HideLoadingPage), delayBeforeHideLoading);
    }

    void HideLoadingPage()
    {
        gameObject.SetActive(false);
    }
}