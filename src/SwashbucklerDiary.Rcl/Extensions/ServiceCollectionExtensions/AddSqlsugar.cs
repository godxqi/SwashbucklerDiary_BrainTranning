using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using System.Diagnostics;
using System.Reflection;

namespace SwashbucklerDiary.Rcl.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlSugarConfig(this IServiceCollection services,
            string connectionString,
            string privacyConnectionString,
            string logConnectionString,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            services.Add<ISqlSugarClient>(sp =>
            {
                var configureExternalServices = new ConfigureExternalServices
                {
                    //注意:  这儿AOP设置不能少
                    EntityService = (p, column) =>
                    {
                        if (column.PropertyName.ToLower() == "id") //是id的设为主键
                        {
                            column.IsPrimarykey = true;
                            if (column.PropertyInfo.PropertyType == typeof(int)) //是id并且是int的是自增
                            {
                                column.IsIdentity = true;
                            }
                        }

                        column.IfTable<DiaryModel>()
                        .ManyToMany(it => it.Tags, typeof(DiaryTagModel), nameof(DiaryTagModel.DiaryId), nameof(DiaryTagModel.TagId))
                        .ManyToMany(it => it.Resources, typeof(DiaryResourceModel), nameof(DiaryResourceModel.DiaryId), nameof(DiaryResourceModel.ResourceUri));

                        column.IfTable<ResourceModel>()
                        .UpdateProperty(it => it.ResourceUri, it =>
                        {
                            it.IsPrimarykey = true;
                        })
                        .ManyToMany(it => it.Diaries, typeof(DiaryResourceModel), nameof(ResourceModel.ResourceUri), nameof(DiaryResourceModel.DiaryId));

                        column.IfTable<TagModel>()
                        .ManyToMany(it => it.Diaries, typeof(DiaryTagModel), nameof(DiaryTagModel.TagId), nameof(DiaryTagModel.DiaryId));

                        //复盘系统的新表。统计全部走 SQL 聚合，缺索引会退化成全表扫描。
                        //同一个索引组名挂在多列上即为联合索引。
                        //组名只用来把多列拉进同一个联合索引，**不控制最终索引名**——
                        //实测 SqlSugar 生成的还是 Index_{表名}_{列名}，别指望这个名字出现在库里。
                        //这里只建普通索引：SqlSugar 的 EntityColumnInfo 没有唯一索引的配置位，
                        //「每天一条日课 / 每周一条周课 / 每月一条月课」由服务层按业务键 upsert 保证。
                        column.IfTable<DailyReviewModel>()
                        .UpdateProperty(it => it.Date, it =>
                        {
                            it.IndexGroupNameList = ["idx_daily_review_date"];
                        })
                        .UpdateProperty(it => it.DiaryId, it =>
                        {
                            it.IndexGroupNameList = ["idx_daily_review_diary"];
                        });

                        column.IfTable<WeeklyReviewModel>()
                        .UpdateProperty(it => it.Year, it =>
                        {
                            it.IndexGroupNameList = ["idx_weekly_review_year_week"];
                        })
                        .UpdateProperty(it => it.WeekOfYear, it =>
                        {
                            it.IndexGroupNameList = ["idx_weekly_review_year_week"];
                        });

                        column.IfTable<WeeklyGoalModel>()
                        .UpdateProperty(it => it.WeeklyReviewId, it =>
                        {
                            it.IndexGroupNameList = ["idx_weekly_goal_review"];
                        })
                        .UpdateProperty(it => it.Status, it =>
                        {
                            it.IndexGroupNameList = ["idx_weekly_goal_status"];
                        });

                        column.IfTable<StumblingBlockModel>()
                        .UpdateProperty(it => it.Year, it =>
                        {
                            it.IndexGroupNameList = ["idx_stumbling_year_week"];
                        })
                        .UpdateProperty(it => it.WeekOfYear, it =>
                        {
                            it.IndexGroupNameList = ["idx_stumbling_year_week"];
                        })
                        .UpdateProperty(it => it.SourceDiaryId, it =>
                        {
                            //(SourceDiaryId, OrderIndex) 是增量同步的对齐键，所以建成联合索引
                            it.IndexGroupNameList = ["idx_stumbling_diary_order"];
                        })
                        .UpdateProperty(it => it.OrderIndex, it =>
                        {
                            it.IndexGroupNameList = ["idx_stumbling_diary_order"];
                        });

                        column.IfTable<MonthlyReviewModel>()
                        .UpdateProperty(it => it.Year, it =>
                        {
                            it.IndexGroupNameList = ["idx_monthly_review_year_month"];
                        })
                        .UpdateProperty(it => it.Month, it =>
                        {
                            it.IndexGroupNameList = ["idx_monthly_review_year_month"];
                        });

                        column.IfTable<LimitingBeliefModel>()
                        .UpdateProperty(it => it.MonthlyReviewId, it =>
                        {
                            it.IndexGroupNameList = ["idx_limiting_belief_review"];
                        });

                        column.IfTable<EnemyModel>()
                        .UpdateProperty(it => it.MonthlyReviewId, it =>
                        {
                            it.IndexGroupNameList = ["idx_enemy_review"];
                        });

                        column.IfTable<VirtueEntryModel>()
                        .UpdateProperty(it => it.Kind, it =>
                        {
                            it.IndexGroupNameList = ["idx_virtue_kind"];
                        })
                        .UpdateProperty(it => it.Date, it =>
                        {
                            it.IndexGroupNameList = ["idx_virtue_date"];
                        })
                        .UpdateProperty(it => it.SourceDiaryId, it =>
                        {
                            it.IndexGroupNameList = ["idx_virtue_diary"];
                        });

                        column.IfTable<ArchiveItemModel>()
                        .UpdateProperty(it => it.Kind, it =>
                        {
                            it.IndexGroupNameList = ["idx_archive_kind"];
                        });

                        column.IfTable<SelfAffirmationModel>()
                        .UpdateProperty(it => it.IsActive, it =>
                        {
                            it.IndexGroupNameList = ["idx_affirmation_active"];
                        });

                        column.IfTable<StateRecordModel>()
                        .UpdateProperty(it => it.OccurredTime, it =>
                        {
                            it.IndexGroupNameList = ["idx_state_occurred"];
                        })
                        .UpdateProperty(it => it.TriggerTagId, it =>
                        {
                            it.IndexGroupNameList = ["idx_state_trigger"];
                        });

                        column.IfTable<LessonModel>()
                        .UpdateProperty(it => it.OccurredTime, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_occurred"];
                        })
                        .UpdateProperty(it => it.PersonTagId, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_person"];
                        })
                        .UpdateProperty(it => it.TriggerTagId, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_trigger"];
                        })
                        .UpdateProperty(it => it.FollowUpDueTime, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_followup"];
                        });

                        column.IfTable<LessonTypeLinkModel>()
                        .UpdateProperty(it => it.LessonId, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_type_lesson"];
                        })
                        .UpdateProperty(it => it.Type, it =>
                        {
                            it.IndexGroupNameList = ["idx_lesson_type_type"];
                        });

                        column.IfTable<DailyEventModel>()
                        .UpdateProperty(it => it.DailyReviewId, it =>
                        {
                            it.IndexGroupNameList = ["idx_daily_event_review"];
                        })
                        .UpdateProperty(it => it.PersonTagId, it =>
                        {
                            it.IndexGroupNameList = ["idx_daily_event_person"];
                        });

                        /***高版C#写法***/
                        //支持string?和string  
                        if (column.IsPrimarykey == false && new NullabilityInfoContext()
                         .Create(p).WriteState is NullabilityState.Nullable)
                        {
                            column.IsNullable = true;
                        }
                    }
                };
                var logConfigureExternalServices = new ConfigureExternalServices
                {
                    //注意:  这儿AOP设置不能少
                    EntityService = (p, column) =>
                    {
                        if (column.PropertyName.ToLower() == "id") //是id的设为主键
                        {
                            column.IsPrimarykey = true;
                            if (column.PropertyInfo.PropertyType == typeof(int)) //是id并且是int的是自增
                            {
                                column.IsIdentity = true;
                            }
                        }

                        column.IfTable<LogModel>()
                        .UpdateProperty(it => it.Timestamp, it =>
                        {
                            it.DataType = "TEXT";
                        })
                        .UpdateProperty(it => it.LevelName, it =>
                        {
                            it.DataType = "TEXT";
                        })
                        .UpdateProperty(it => it.Message, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.MessageTemplate, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.Exception, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.Properties, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.SourceContext, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.MachineName, it =>
                        {
                            it.DataType = "TEXT";
                            it.IsNullable = true;
                        })
                        .UpdateProperty(it => it.ThreadId, it =>
                        {
                            it.DataType = "INTEGER";
                            it.IsNullable = true;
                        });
                    }
                };
                var configs = new List<ConnectionConfig>(){
                    new ConnectionConfig(){
                        ConfigId = SQLiteConstants.MainDatabaseFilename,
                        DbType = DbType.Sqlite,
                        ConnectionString = connectionString,
                        InitKeyType = InitKeyType.Attribute,
                        IsAutoCloseConnection = true,
                        ConfigureExternalServices =configureExternalServices,
                        MoreSettings=new ConnMoreSettings()
                        {
                            IsNoReadXmlDescription=true
                        }
                    },
                    new ConnectionConfig(){
                        ConfigId = SQLiteConstants.PrivacyDatabaseFilename,
                        DbType = DbType.Sqlite,
                        ConnectionString = privacyConnectionString,
                        InitKeyType = InitKeyType.Attribute,
                        IsAutoCloseConnection = true,
                        ConfigureExternalServices =configureExternalServices,
                        MoreSettings=new ConnMoreSettings()
                        {
                            IsNoReadXmlDescription=true
                        }
                    },
                    new ConnectionConfig(){
                        ConfigId = SQLiteConstants.LogDatabaseFilename,
                        DbType = DbType.Sqlite,
                        ConnectionString = logConnectionString,
                        InitKeyType = InitKeyType.Attribute,
                        IsAutoCloseConnection = true,
                        ConfigureExternalServices = logConfigureExternalServices,
                        MoreSettings=new ConnMoreSettings()
                        {
                            IsNoReadXmlDescription=true
                        }
                    }
                };
                var dataConfigs = configs.Where(c => c.ConfigId.ToString() != SQLiteConstants.LogDatabaseFilename).ToList();
                void ConfigAction(SqlSugarClient db)
                {
                    //单例参数配置，所有上下文生效
                    foreach (var config in dataConfigs)
                    {
#if DEBUG
                        db.GetConnection(config.ConfigId).Aop.OnLogExecuting = (sql, pars) =>
                        {
                            //Debug.WriteLine(sql);//输出sql
                            Debug.WriteLine(UtilMethods.GetSqlString(DbType.Sqlite, sql, pars));//输出sql
                        };
#endif
                        // UpdateTime 兜底。业务代码里 30 多处手写 UpdateTime，漏一处就会漏同步，
                        // 所以统一在这里刷新。两个例外：
                        // 1. 同步导入时（SyncContext），必须保留对象自带的 UpdateTime，否则 LWW 失效；
                        // 2. Insert 时只在默认值的情况下补，避免覆盖导入对象的时间。
                        // 注意：Updateable(entity).UpdateColumns(columns) 只写指定列，
                        // 若 columns 不含 UpdateTime，这里设置的值不会落库。这类调用点需要显式带上 UpdateTime。
                        db.GetConnection(config.ConfigId).Aop.DataExecuting = (oldValue, entityInfo) =>
                        {
                            if (entityInfo.EntityValue is not BaseModel model)
                            {
                                return;
                            }

                            if (entityInfo.PropertyName != nameof(BaseModel.UpdateTime))
                            {
                                return;
                            }

                            // SqlSugarCoreNoDrive.Aot 把 DataOperationType 枚举裁掉了（编译期找不到类型），
                            // 只能按运行时字符串判断。实测真机返回的是 "UpdateByObject" 这类带后缀的值，
                            // 不是文档里的 "Update"，所以用前缀匹配，同时兼容数值形式（Add=0 / Update=1）。
                            // 先判属性名再判操作类型，避免每次写入为每个属性付一次字符串比较。
                            string operation = entityInfo.OperationType.ToString();
                            bool isAdd = operation == "0"
                                || operation.StartsWith("Add", StringComparison.Ordinal);
                            bool isUpdate = operation == "1"
                                || operation.StartsWith("Update", StringComparison.Ordinal);

                            SyncDiagnostics.LastOperationType = operation;
                            SyncDiagnostics.LastPropertyName = entityInfo.PropertyName;

                            if (isAdd)
                            {
                                if (model.UpdateTime == default)
                                {
                                    entityInfo.SetValue(DateTime.Now);
                                }
                            }
                            else if (isUpdate && !SyncContext.SuppressUpdateTimeRefresh)
                            {
                                entityInfo.SetValue(DateTime.Now);
                            }
                        };
                        if (OperatingSystem.IsBrowser())
                        {
                            db.GetConnection(config.ConfigId).Aop.OnLogExecuted = async (sql, pars) =>
                            {
                                IAppFileSystem? appFileSystem = sp.GetService<IAppFileSystem>();
                                if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                                    && appFileSystem is not null)
                                {
                                    await Task.Delay(500);
                                    await appFileSystem.SyncFS();
                                }
                            };
                        }
                    }
                }

                ISqlSugarClient sqlSugar = serviceLifetime == ServiceLifetime.Singleton
                ? new SqlSugarScope(configs, ConfigAction)
                : new SqlSugarClient(configs, ConfigAction);

                // 创建表
                Type[] types = {
                    typeof(DiaryModel),
                    typeof(TagModel),
                    typeof(DiaryTagModel),
                    typeof(UserAchievementModel),
                    typeof(UserStateModel),
                    typeof(LocationModel),
                    typeof(ResourceModel),
                    typeof(DiaryResourceModel),
                    // 复盘系统
                    typeof(DailyReviewModel),
                    typeof(WeeklyReviewModel),
                    typeof(WeeklyGoalModel),
                    typeof(StumblingBlockModel),
                    typeof(MonthlyReviewModel),
                    typeof(LimitingBeliefModel),
                    typeof(EnemyModel),
                    typeof(VirtueEntryModel),
                    typeof(SelfAffirmationModel),
                    typeof(ArchiveItemModel),
                    typeof(MacroGoalModel),
                    typeof(StateRecordModel),
                    typeof(LessonModel),
                    typeof(LessonTypeLinkModel),
                    typeof(DailyEventModel),
                    // 同步墓碑
                    typeof(SyncGraveModel),
                };

                foreach (var config in dataConfigs)
                {
                    sqlSugar.AsTenant().GetConnection(config.ConfigId).CodeFirst.InitTables(types);
                }

                sqlSugar.AsTenant().GetConnection(SQLiteConstants.LogDatabaseFilename).CodeFirst.InitTables<LogModel>();
                return sqlSugar;
            }, serviceLifetime);//这边是SqlSugarScope用AddSingleton
            return services;
        }

        private static void Add<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory, ServiceLifetime lifetime) where TService : class
        {
            services.Add(new ServiceDescriptor(typeof(TService), implementationFactory, lifetime));
        }
    }
}
