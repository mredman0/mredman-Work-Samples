using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class PurchaseManager : MonoBehaviour
{
    public static PurchaseManager Instance;

	public event Action Initialized;
	public event Action InitializationFailed;

	public event Action<string, Product> PurchaseProcessed;
	public event Action<IEnumerable<Product>, PurchaseFailureReason> PurchaseFailed;

	public event Action<string> RestoreSucceeded;
	public event Action<string> RestoreFailed;

	public event Action<bool> AdFreeChanged;
	public event Action<int> HintsPurchased;
	public event Action DailyPuzzleAccessChanged;

	[Header("Settings")]
	public bool UseFakeStore;
	public FakeStoreUIMode FakeStoreUIMode = FakeStoreUIMode.DeveloperUser;

	[Header("State")]
	public bool IsInitialized = false;

	private StoreController StoreController;
	private SynchronizationContext MainThreadContext;

	public const string ID_PREFIX = "com.redprismgames.chromasphere.";

	private static readonly string[] NonConsumableProductIds =
	{
		"ad_free",
		"daily_puzzles_beginner",
		"daily_puzzles_intermediate",
		"daily_puzzles_expert",
		"daily_puzzles_grandmaster",
		"daily_puzzles_all",
	};

	// Start is called before the first frame update
	void Start()
    {
        if(Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
		DontDestroyOnLoad(gameObject);
		MainThreadContext = SynchronizationContext.Current;

#if DEMO
		return;
#endif

		if (UnityServicesManager.Instance.Initialized)
		{
			Initialize();
		}
		UnityServicesManager.Instance.ServicesInitialized += Initialize;
    }

	#region Startup
	private async void Initialize()
	{
		if (SynchronizationContext.Current != MainThreadContext)
		{
			MainThreadContext.Post(_ => Initialize(), null);
			return;
		}

		//var purchasingModule = StandardPurchasingModule.Instance();
		//if(UseFakeStore)
		//{
		//	purchasingModule.useFakeStoreAlways = UseFakeStore;
		//	purchasingModule.useFakeStoreUIMode = FakeStoreUIMode;
		//	Debug.Log("Using Fake Store...");
		//}

		Debug.Log("Initializing StoreController");
		StoreController = UnityIAPServices.StoreController();

		StoreController.OnPurchasePending += OnPurchasePending;
		StoreController.OnPurchaseFailed += OnPurchaseFailed;

		StoreController.OnProductsFetched += OnProductsFetched;
		StoreController.OnPurchasesFetched += OnPurchasesFetched;
		StoreController.OnProductsFetchFailed += OnProductsFetchFailed;
		StoreController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
		StoreController.OnCheckEntitlement += OnCheckEntitlement;

		var productsToFetch = new List<ProductDefinition>()
		{
			new ProductDefinition($"{ID_PREFIX}ad_free", $"{ID_PREFIX}ad_free", ProductType.NonConsumable, true),

			new ProductDefinition($"{ID_PREFIX}hints_small", $"{ID_PREFIX}hints_small", ProductType.Consumable, true, new PayoutDefinition(PayoutType.Resource, "hint", 10)),
			new ProductDefinition($"{ID_PREFIX}hints_large", $"{ID_PREFIX}hints_large", ProductType.Consumable, true, new PayoutDefinition(PayoutType.Resource, "hint", 100)),

			new ProductDefinition($"{ID_PREFIX}daily_puzzles_beginner", $"{ID_PREFIX}daily_puzzles_beginner", ProductType.NonConsumable, true),
			new ProductDefinition($"{ID_PREFIX}daily_puzzles_intermediate", $"{ID_PREFIX}daily_puzzles_intermediate", ProductType.NonConsumable, true),
			new ProductDefinition($"{ID_PREFIX}daily_puzzles_expert", $"{ID_PREFIX}daily_puzzles_expert", ProductType.NonConsumable, true),
			new ProductDefinition($"{ID_PREFIX}daily_puzzles_grandmaster", $"{ID_PREFIX}daily_puzzles_grandmaster", ProductType.NonConsumable, true),
			new ProductDefinition($"{ID_PREFIX}daily_puzzles_all", $"{ID_PREFIX}daily_puzzles_all", ProductType.NonConsumable, true),
		};

		await StoreController.Connect();
		StoreController.FetchProducts(productsToFetch);
	}

	private void OnProductsFetched(List<Product> products)
	{
		StoreController.FetchPurchases();
	}
	private void OnProductsFetchFailed(ProductFetchFailed failure)
	{
		Debug.LogError($"Failed to fetch products: {string.Join(',', failure.FailedFetchProducts)}... {failure.FailureReason}");
		InitializationFailed?.Invoke();
	}

	private void OnPurchasesFetched(Orders orders)
	{
		CacheOrders(orders);

		var productsToCheck = NonConsumableProductIds
			.Select(id => StoreController.GetProductById($"{ID_PREFIX}{id}"))
			.Where(p => p != null)
			.ToList();

		if (productsToCheck.Count == 0)
		{
			CompleteInitialization();
			return;
		}

		RemainingStartupEntitlementChecks = productsToCheck.Count;
		StartupEntitlementsPending = true;
		foreach (var product in productsToCheck)
		{
			StoreController.CheckEntitlement(product);
		}
	}
	private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
	{
		Debug.LogError($"Failed to fetch purchases... {failure.FailureReason}");
		InitializationFailed?.Invoke();
	}

	private void CompleteInitialization()
	{
		Debug.Log("UnityPurchasing initialized");
		IsInitialized = true;
		Initialized?.Invoke();
	}
	#endregion

	#region Events
	private void OnPurchasePending(PendingOrder order)
	{
		CacheOrder(order);
		var allProcessed = true;
		foreach (var item in order.CartOrdered.Items())
		{
			allProcessed &= ProcessOrderItem(item);
		}
		if (allProcessed)
		{
			StoreController.ConfirmPurchase(order);
		}
	}

	private void OnPurchaseFailed(FailedOrder order)
	{
		PurchaseFailed?.Invoke(order.CartOrdered.Items().Select(i => i.Product), order.FailureReason);
	}

	private bool StartupEntitlementsPending = false;
	private int RemainingStartupEntitlementChecks;
	private void OnCheckEntitlement(Entitlement entitlement)
	{
		var id = entitlement.Product.definition.id;
		if (entitlement.Status == EntitlementStatus.FullyEntitled)
		{
			OwnedNonConsumables.Add(id);
		}
		else
		{
			OwnedNonConsumables.Remove(id);
		}

		if (id == $"{ID_PREFIX}ad_free" && entitlement.Status == EntitlementStatus.FullyEntitled)
		{
			Debug.Log("Ad-Free product owned, applying ad-free state.");
			AdFreeChanged?.Invoke(true);
		}

		var shortId = id.Replace(ID_PREFIX, "");
		if (IsInitialized && shortId.StartsWith("daily_puzzles_"))
		{
			DailyPuzzleAccessChanged?.Invoke();
		}

		if (StartupEntitlementsPending)
		{
			RemainingStartupEntitlementChecks--;
			if (RemainingStartupEntitlementChecks <= 0)
			{
				StartupEntitlementsPending = false;
				CompleteInitialization();
			}
		}
	}
	#endregion

	#region Purchase Processing
	private bool ProcessOrderItem(CartItem item)
	{
		var payouts = item.Product.definition.payouts;
		var id = item.Product.definition.id.Replace(ID_PREFIX, "");
		bool success;
		if (payouts != null && payouts.Any())
		{
			success = ProcessPurchaseByPayouts(payouts);
			if (success)
			{
				PurchaseProcessed?.Invoke(id, item.Product);
			}
			return success;
		}
		success = ProcessPurchaseById(id);
		if (success)
		{
			PurchaseProcessed?.Invoke(id, item.Product);
		}
		return success;
	}
	private bool ProcessPurchaseByPayouts(IEnumerable<PayoutDefinition> payouts)
	{
		foreach(var payout in payouts)
		{
			if(payout.subtype == "hint")
			{
				Debug.Log($"Granting {(int)payout.quantity} hints from purchase");
				HintsPurchased?.Invoke((int)payout.quantity);
			}
		}
		return true;
	}
	private bool ProcessPurchaseById(string id)
	{
		if(id == "ad_free")
		{
			Debug.Log($"Granting Ad-Free from purchase");
			OwnedNonConsumables.Add($"{ID_PREFIX}ad_free");
			AdFreeChanged?.Invoke(true);
			return true;
		}

		if(id == "daily_puzzles_beginner" ||
			id == "daily_puzzles_intermediate" ||
			id == "daily_puzzles_expert" ||
			id == "daily_puzzles_grandmaster" ||
			id == "daily_puzzles_all")
		{
			Debug.Log($"Daily puzzle access changed due to purchase");
			OwnedNonConsumables.Add($"{ID_PREFIX}{id}");
			DailyPuzzleAccessChanged?.Invoke();
			return true;
		}

		return true;
	}

	public int CountAccessibleDailyPuzzles()
	{
		if(NonConsumableOwned("daily_puzzles_all"))
		{
			return 12;
		}
		var accessible = 4;
		if (NonConsumableOwned("daily_puzzles_beginner")) { accessible += 2; }
		if (NonConsumableOwned("daily_puzzles_intermediate")) { accessible += 2; }
		if (NonConsumableOwned("daily_puzzles_expert")) { accessible += 2; }
		if (NonConsumableOwned("daily_puzzles_grandmaster")) { accessible += 2; }
		return accessible;
	}
#endregion

#region API
	public void InitiatePurchase(string productId) => StoreController.PurchaseProduct($"{ID_PREFIX}{productId}");

	public Product GetProduct(string productId) => StoreController?.GetProductById($"{ID_PREFIX}{productId}");

	public string GetPurchaseReceipt(string productId)
	{
		PurchaseReceipts.TryGetValue($"{ID_PREFIX}{productId}", out var receipt);
		return receipt;
	}

	public string GetTransactionId(string productId)
	{
		PurchaseTransactionIds.TryGetValue($"{ID_PREFIX}{productId}", out var transactionId);
		return transactionId;
	}

	public bool RestorePurchases()
	{
#if UNITY_EDITOR
		var success = UnityEngine.Random.Range(0, 2) % 2 == 0;
		if(success)
		{
			Debug.Log($"(TEST) Transactions have been restored");
			RestoreSucceeded?.Invoke("Success");
		}
		else
		{
			Debug.LogError($"(TEST) Failed to restore transactions");
			RestoreFailed?.Invoke("Failure");
		}
		return true;
#elif UNITY_ANDROID || UNITY_IOS
		StoreController.RestoreTransactions((result, resultStr) => {
			if (result)
			{
				Debug.Log($"Transactions have been restored");
				RestoreSucceeded?.Invoke(resultStr);
			}
			else
			{
				Debug.LogError($"Failed to restore transactions: {resultStr}");
				RestoreFailed?.Invoke(resultStr);
			}
		});
		return true;
#else
		return false;
#endif
	}

	private HashSet<string> OwnedNonConsumables = new HashSet<string>();
	private readonly Dictionary<string, string> PurchaseReceipts = new Dictionary<string, string>();
	private readonly Dictionary<string, string> PurchaseTransactionIds = new Dictionary<string, string>();
	public bool NonConsumableOwned(string productId) => OwnedNonConsumables.Contains($"{ID_PREFIX}{productId}");

	private void CacheOrders(Orders orders)
	{
		if (orders == null)
		{
			return;
		}
		if (orders.ConfirmedOrders != null)
		{
			foreach (var order in orders.ConfirmedOrders)
			{
				CacheOrder(order);
			}
		}
		if (orders.PendingOrders != null)
		{
			foreach (var order in orders.PendingOrders)
			{
				CacheOrder(order);
			}
		}
	}

	private void CacheOrder(Order order)
	{
		if (order?.Info == null || order.CartOrdered == null)
		{
			return;
		}

		var receipt = order.Info.Receipt;
		var transactionId = order.Info.TransactionID;
#if UNITY_IOS
		if (string.IsNullOrEmpty(transactionId))
		{
			transactionId = order.Info.Apple?.jwsRepresentation;
		}
#endif

		foreach (var item in order.CartOrdered.Items())
		{
			var fullId = item.Product?.definition?.id;
			if (string.IsNullOrEmpty(fullId))
			{
				continue;
			}
			if (!string.IsNullOrEmpty(receipt))
			{
				PurchaseReceipts[fullId] = receipt;
			}
			if (!string.IsNullOrEmpty(transactionId))
			{
				PurchaseTransactionIds[fullId] = transactionId;
			}
		}
	}
#endregion
}
