# Presentation Queue for Unity

Ekranda bir süre yer kaplayan gösterimleri (banner, açılış kartı, ödül animasyonu...) **sırayla** oynatan UniTask tabanlı servis. İki gösterim hiçbir zaman aynı anda ekrana çıkmaz; VContainer ile tekil servis olarak kaydedilir.

## Kurulum

Unity 2022.3 veya üzeri ve Git gerekir. Unity Package Manager > **Add package from git URL...** üzerinden sırayla şu adresleri ekleyin:

1. UniTask: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. VContainer: `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.19.0`
3. Presentation Queue: `https://github.com/kameryurdakull/unity-presentation-queue.git?path=/Packages/com.kameryurdakull.presentation-queue#v1.0.0`

Unity, bir Git paketinin `package.json` dosyasından başka Git paketlerini otomatik kurmaz. Bu nedenle UniTask ve VContainer'ı Presentation Queue'dan önce projeye eklemek gerekir.

## Kullanım

Bir `LifetimeScope` içinde servisi kaydedin:

```csharp
using Presentations;
using VContainer;
using VContainer.Unity;

public sealed class GameScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterPresentationQueue();
    }
}
```

Gösterimi `IPresentation` olarak yazıp kuyruğa verin:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Presentations;
using UnityEngine;
using VContainer;

public sealed class BannerView : MonoBehaviour, IPresentation
{
    [SerializeField] private CanvasGroup _panel;

    [Inject] private IPresentationQueue _queue;

    public void Show() => _queue.EnqueueAsync(this, priority: 10).Forget();

    public async UniTask PlayAsync(CancellationToken cancellation)
    {
        await _panel.DOFade(1f, 0.3f).ToUniTask(cancellationToken: cancellation);
        await UniTask.Delay(1500, cancellationToken: cancellation);
        await _panel.DOFade(0f, 0.3f).ToUniTask(cancellationToken: cancellation);
    }
}
```

Bu örnek DOTween ve UniTask'ın DOTween desteğini (`UNITASK_DOTWEEN_SUPPORT`) kullanır; paket DOTween'e bağımlı değildir. Bir metodu doğrudan da kuyruğa verebilirsiniz: `_queue.EnqueueAsync(PlayIntroAsync)`.

## Davranış

- Oynayan gösterim yarıda kesilmez. Bekleyenler arasında yüksek `priority` önce oynar, eşitler geliş sırasını korur.
- Boşta bekleyen kuyruğa ilk gösterim bir sonraki karede başlar; böylece **aynı karede** kuyruğa giren gösterimler önceliğe göre dizilir.
- `EnqueueAsync` gösterim bittiğinde tamamlanır, düşürülürse iptal olur. Token iptal edilirse bekleyen gösterim düşer, oynayan durur.
- `WaitUntilIdleAsync` ve `BusyChanged` ile kuyruğun boşaldığı an beklenir; oyun akışı (spawn, dalga, saat) buna bağlanabilir.
- `Clear` bekleyenleri düşürür, oynayanı durdurur. Hata fırlatan gösterim loglanır, sıra kilitlenmez.
- Paket, Unity ana iş parçacığında kullanım için tasarlanmıştır. Kapsam yok edildiğinde VContainer kuyruğu da dispose eder ve oynayan gösterim durur.

Package Manager'daki **Samples > Basic Usage > Import** ile çalışır bir VContainer örneği alın. İçe aktarılan `ExampleLifetimeScope` bileşenini sahnedeki boş bir GameObject'e ekleyin.

Paketin kaynakları: [`Packages/com.kameryurdakull.presentation-queue`](Packages/com.kameryurdakull.presentation-queue). EditMode testleri paket içinde bulunur.
