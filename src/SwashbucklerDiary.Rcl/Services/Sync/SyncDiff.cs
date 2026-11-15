namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步差异计算。纯函数：输入本地快照 + 远端 manifest + 上次同步时间，输出要做的事。
    /// <para>
    /// 刻意不碰数据库、文件系统和网络，这样阶段 3 的执行层可以单独测试，diff 本身也能穷举边界。
    /// </para>
    /// </summary>
    public static class SyncDiff
    {
        public static SyncPlan Compute(
            SyncManifest? remote,
            SyncLocalSnapshot local,
            DateTime lastSessionStart)
        {
            var plan = new SyncPlan();
            var remoteManifest = remote ?? new SyncManifest();

            // 远端 manifest 是外部输入。字段缺失时初始化器生效，但显式 `"diaries": null`
            // 会把属性置空——一条坏数据就能让整个同步 NRE。这里统一兜底。
            remoteManifest.Diaries ??= [];
            remoteManifest.Review ??= [];
            remoteManifest.Assets ??= [];
            remoteManifest.Graves ??= [];

            long lastMs = SyncTime.ToUnixMsOrMin(lastSessionStart);

            // 已经由墓碑处理过的 key，不再参与对象 diff。
            var handled = new HashSet<string>(StringComparer.Ordinal);

            ApplyRemoteGraves(plan, remoteManifest, local, handled);
            CollectLocalGraves(plan, local, lastMs, handled);

            DiffCollection(plan, SyncEntityType.Diary, SyncEntityKeys.DiaryPrefix,
                local.Diaries, remoteManifest.Diaries, lastMs, handled);

            DiffCollection(plan, SyncEntityType.Review, SyncEntityKeys.ReviewPrefix,
                local.Review, remoteManifest.Review, lastMs, handled);

            DiffCollection(plan, SyncEntityType.Asset, SyncEntityKeys.AssetPrefix,
                local.Assets, remoteManifest.Assets, lastMs, handled);

            DiffSingle(plan, SyncEntityType.Location, SyncEntityKeys.LocationKey,
                local.Locations, remoteManifest.Locations, lastMs, handled);

            return plan;
        }

        /// <summary>
        /// 应用远端墓碑。规则：
        /// <list type="bullet">
        /// <item>本地没这个对象 → 无事可做；</item>
        /// <item>本地对象不比墓碑新 → 删除本地；</item>
        /// <item>本地对象在墓碑之后又被编辑过 → 视为"复活"，保留并撤销墓碑。</item>
        /// </list>
        /// </summary>
        private static void ApplyRemoteGraves(
            SyncPlan plan,
            SyncManifest remote,
            SyncLocalSnapshot local,
            HashSet<string> handled)
        {
            foreach (var (key, deletedAt) in remote.Graves)
            {
                handled.Add(key);

                SyncObjectMeta? meta = ResolveLocal(local, key);
                if (meta is null)
                {
                    continue;
                }

                var type = SyncEntityKeys.GetType(key);
                if (meta.UpdateTime <= deletedAt)
                {
                    plan.Add(type, key, SyncAction.DeleteLocal, meta.UpdateTime, deletedAt);
                }
                else
                {
                    // 本地在删除之后编辑过：撤销墓碑，并确保远端拿回这个对象。
                    plan.GravesToWithdraw.Add(key);
                    plan.Add(type, key, SyncAction.Upload, meta.UpdateTime, deletedAt);
                }
            }
        }

        /// <summary>
        /// 收集本轮的本地墓碑。只用上次同步开始时间之后产生的墓碑——
        /// 更早的上一轮已经推过，重复推送只会让 manifest 越滚越大。
        /// </summary>
        private static void CollectLocalGraves(
            SyncPlan plan,
            SyncLocalSnapshot local,
            long lastMs,
            HashSet<string> handled)
        {
            foreach (var (key, deletedAt) in local.Graves)
            {
                if (deletedAt <= lastMs || !handled.Add(key))
                {
                    continue;
                }

                plan.GravesToPublish.Add(new SyncGraveEntry(key, deletedAt));
                plan.Add(SyncEntityKeys.GetType(key), key, SyncAction.DeleteRemote, 0, deletedAt);
            }
        }

        private static void DiffCollection(
            SyncPlan plan,
            SyncEntityType type,
            string prefix,
            IReadOnlyDictionary<string, SyncObjectMeta> local,
            IReadOnlyDictionary<string, SyncObjectMeta> remote,
            long lastMs,
            HashSet<string> handled)
        {
            var keys = new HashSet<string>(local.Keys, StringComparer.Ordinal);
            keys.UnionWith(remote.Keys);

            foreach (string bareKey in keys)
            {
                string key = prefix + bareKey;
                if (!handled.Add(key))
                {
                    continue;
                }

                var localMeta = local.GetValueOrDefault(bareKey);
                var remoteMeta = remote.GetValueOrDefault(bareKey);
                var action = Decide(localMeta, remoteMeta, lastMs);
                if (action is null)
                {
                    continue;
                }

                plan.Add(
                    type,
                    key,
                    action.Value,
                    localMeta?.UpdateTime ?? 0,
                    remoteMeta?.UpdateTime ?? 0);
            }
        }

        private static void DiffSingle(
            SyncPlan plan,
            SyncEntityType type,
            string key,
            SyncObjectMeta? localMeta,
            SyncObjectMeta? remoteMeta,
            long lastMs,
            HashSet<string> handled)
        {
            if (!handled.Add(key))
            {
                return;
            }

            var action = Decide(localMeta, remoteMeta, lastMs);
            if (action is null)
            {
                return;
            }

            plan.Add(
                type,
                key,
                action.Value,
                localMeta?.UpdateTime ?? 0,
                remoteMeta?.UpdateTime ?? 0);
        }

        /// <summary>
        /// 对象级冲突决策。
        /// <para>
        /// 冲突的判定是"两端都在上次同步开始之后改过，且内容哈希不同"。
        /// 只比时间戳不够：A 端改了标题、B 端改了正文但时间戳更旧时，静默覆盖会丢编辑。
        /// 判定为冲突后，执行层会先把本地版本保存为副本，再应用远端版本。
        /// </para>
        /// </summary>
        private static SyncAction? Decide(
            SyncObjectMeta? localMeta,
            SyncObjectMeta? remoteMeta,
            long lastMs)
        {
            if (localMeta is null && remoteMeta is null)
            {
                return null;
            }

            if (localMeta is null)
            {
                return SyncAction.Download;
            }

            if (remoteMeta is null)
            {
                return SyncAction.Upload;
            }

            if (string.Equals(localMeta.Hash, remoteMeta.Hash, StringComparison.Ordinal))
            {
                return null;
            }

            bool localChanged = localMeta.UpdateTime > lastMs;
            bool remoteChanged = remoteMeta.UpdateTime > lastMs;
            if (localChanged && remoteChanged)
            {
                return SyncAction.Conflict;
            }

            return localMeta.UpdateTime > remoteMeta.UpdateTime
                ? SyncAction.Upload
                : SyncAction.Download;
        }

        private static SyncObjectMeta? ResolveLocal(SyncLocalSnapshot local, string key)
        {
            var type = SyncEntityKeys.GetType(key);
            var bareKey = SyncEntityKeys.GetBareKey(key);

            return type switch
            {
                SyncEntityType.Diary => local.Diaries.GetValueOrDefault(bareKey),
                SyncEntityType.Review => local.Review.GetValueOrDefault(bareKey),
                SyncEntityType.Asset => local.Assets.GetValueOrDefault(bareKey),
                // 地点/复盘的行墓碑（带行 Id）在整表快照里没有对应对象，
                // 交给执行器在行级合并时处理；这里返回 null 表示"本地无此对象"。
                SyncEntityType.Location => key == SyncEntityKeys.LocationKey ? local.Locations : null,
                _ => null,
            };
        }
    }
}
