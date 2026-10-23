/// <summary>零信任範例的存取情境，範例與整合測試共用</summary>
/// <remarks>每次讀取都回傳新的訊號物件，呼叫端修改不會影響其他情境</remarks>
public static class ZeroTrustScenarios
{
    /// <summary>公司筆電，平常的使用模式</summary>
    public static JsonObject OfficeLaptop
    {
        get
        {
            return new JsonObject
            {
                ["user"] = new JsonObject
                {
                    ["role"] = "accountant",
                    ["mfa_age_minutes"] = 12,
                    ["failed_logins_24h"] = 0,
                },
                ["device"] = new JsonObject
                {
                    ["managed"] = true,
                    ["edr_healthy"] = true,
                    ["os_patch_lag_days"] = 3,
                    ["disk_encrypted"] = true,
                },
                ["network"] = new JsonObject
                {
                    ["ip_reputation"] = "clean",
                    ["asn_type"] = "corporate",
                    ["geo"] = "Taipei, TW",
                    ["tor_or_vpn"] = false,
                },
                ["behavior"] = new JsonObject
                {
                    ["impossible_travel"] = false,
                    ["new_device"] = false,
                    ["hour_local"] = 10,
                    ["typical_hours"] = "08-19",
                },
                ["resource"] = new JsonObject
                {
                    ["name"] = "erp/invoices",
                    ["sensitivity"] = "internal",
                    ["method"] = "GET",
                },
            };
        }
    }

    /// <summary>受管裝置，但從新地點登入並匯出薪資資料</summary>
    public static JsonObject NewLocationExport
    {
        get
        {
            return new JsonObject
            {
                ["user"] = new JsonObject
                {
                    ["role"] = "hr_manager",
                    ["mfa_age_minutes"] = 600,
                    ["failed_logins_24h"] = 1,
                },
                ["device"] = new JsonObject
                {
                    ["managed"] = true,
                    ["edr_healthy"] = true,
                    ["os_patch_lag_days"] = 9,
                },
                ["network"] = new JsonObject
                {
                    ["ip_reputation"] = "clean",
                    ["asn_type"] = "residential",
                    ["geo"] = "Osaka, JP",
                    ["tor_or_vpn"] = false,
                },
                ["behavior"] = new JsonObject
                {
                    ["impossible_travel"] = false,
                    ["new_device"] = false,
                    ["new_location"] = true,
                    ["hour_local"] = 21,
                },
                ["resource"] = new JsonObject
                {
                    ["name"] = "hr/salaries/export",
                    ["sensitivity"] = "confidential",
                    ["method"] = "GET",
                },
            };
        }
    }

    /// <summary>外包人員以沒有 EDR、更新落後的個人裝置讀取內部 wiki</summary>
    public static JsonObject ContractorByod
    {
        get
        {
            return new JsonObject
            {
                ["user"] = new JsonObject
                {
                    ["role"] = "contractor",
                    ["mfa_age_minutes"] = 5,
                    ["failed_logins_24h"] = 0,
                },
                ["device"] = new JsonObject
                {
                    ["managed"] = false,
                    ["edr_healthy"] = false,
                    ["os_patch_lag_days"] = 60,
                    ["disk_encrypted"] = "unknown",
                },
                ["network"] = new JsonObject
                {
                    ["ip_reputation"] = "clean",
                    ["asn_type"] = "residential",
                    ["geo"] = "Taichung, TW",
                    ["tor_or_vpn"] = false,
                },
                ["behavior"] = new JsonObject
                {
                    ["impossible_travel"] = false,
                    ["new_device"] = false,
                    ["hour_local"] = 14,
                },
                ["resource"] = new JsonObject
                {
                    ["name"] = "wiki/engineering",
                    ["sensitivity"] = "internal",
                    ["method"] = "GET",
                },
            };
        }
    }

    /// <summary>CI 服務帳號在排定的凌晨時段，從允許清單內的雲端 ASN 部署正式環境</summary>
    public static JsonObject NightlyDeploy
    {
        get
        {
            return new JsonObject
            {
                ["user"] = new JsonObject
                {
                    ["role"] = "service_account:ci-deployer",
                    ["interactive"] = false,
                    ["mfa"] = "n/a (workload identity, OIDC)",
                    ["failed_logins_24h"] = 0,
                },
                ["device"] = new JsonObject
                {
                    ["managed"] = true,
                    ["type"] = "ephemeral ci runner",
                    ["image_signed"] = true,
                },
                ["network"] = new JsonObject
                {
                    ["ip_reputation"] = "clean",
                    ["asn_type"] = "cloud (allow-listed)",
                    ["geo"] = "Tokyo, JP",
                    ["tor_or_vpn"] = false,
                },
                ["behavior"] = new JsonObject
                {
                    ["impossible_travel"] = false,
                    ["new_device"] = true,
                    ["hour_local"] = 3,
                    ["matches_schedule"] = "nightly deploy 03:00",
                },
                ["resource"] = new JsonObject
                {
                    ["name"] = "k8s/prod/deployments",
                    ["sensitivity"] = "restricted",
                    ["method"] = "PATCH",
                },
            };
        }
    }

    /// <summary>非受管裝置經由 Tor、不可能的移動距離，並要刪除管理員帳號</summary>
    public static JsonObject TorAdminDelete
    {
        get
        {
            return new JsonObject
            {
                ["user"] = new JsonObject
                {
                    ["role"] = "domain_admin",
                    ["mfa_age_minutes"] = 700,
                    ["failed_logins_24h"] = 14,
                },
                ["device"] = new JsonObject
                {
                    ["managed"] = false,
                    ["edr_healthy"] = false,
                    ["os_patch_lag_days"] = 190,
                    ["disk_encrypted"] = false,
                },
                ["network"] = new JsonObject
                {
                    ["ip_reputation"] = "tor_exit",
                    ["asn_type"] = "hosting",
                    ["geo"] = "Lagos, NG",
                    ["tor_or_vpn"] = true,
                },
                ["behavior"] = new JsonObject
                {
                    ["impossible_travel"] = true,
                    ["new_device"] = true,
                    ["hour_local"] = 3,
                    ["typical_hours"] = "08-19",
                    ["last_seen_geo"] = "Taipei, TW 40 min ago",
                },
                ["resource"] = new JsonObject
                {
                    ["name"] = "iam/admin/users",
                    ["sensitivity"] = "restricted",
                    ["method"] = "DELETE",
                },
            };
        }
    }

    /// <summary>所有情境，依風險由低到高排列</summary>
    public static IReadOnlyList<(string Name, JsonObject Signals)> All
    {
        get
        {
            return [
                ("office laptop, usual pattern", OfficeLaptop),
                ("CI service account, scheduled 3 a.m. deploy", NightlyDeploy),
                ("managed device, new location, salary export", NewLocationExport),
                ("contractor's own device without EDR, internal wiki", ContractorByod),
                ("unmanaged device via Tor, impossible travel, admin delete", TorAdminDelete),
            ];
        }
    }
}