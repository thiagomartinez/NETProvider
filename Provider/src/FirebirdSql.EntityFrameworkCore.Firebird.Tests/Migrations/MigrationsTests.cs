﻿/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/blob/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

//$Authors = Jiri Cincura (jiri@cincura.net)

using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Migrations
{
#pragma warning disable EF1001
	public class MigrationsTests : EntityFrameworkCoreTestsBase
	{
		[Test]
		public void CreateTable()
		{
			var operation = new CreateTableOperation
			{
				Name = "People",
				Columns =
				{
						new AddColumnOperation
						{
							Name = "Id",
							Table = "People",
							ClrType = typeof(int),
							IsNullable = false,
							[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.None,
						},
						new AddColumnOperation
						{
							Name = "Id_Identity",
							Table = "People",
							ClrType = typeof(int),
							IsNullable = false,
							[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.IdentityColumn,
						},
						new AddColumnOperation
						{
							Name = "Id_Sequence",
							Table = "People",
							ClrType = typeof(int),
							IsNullable = false,
							[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.SequenceTrigger,
						},
						new AddColumnOperation
						{
							Name = "EmployerId",
							Table = "People",
							ClrType = typeof(int),
							IsNullable = true,
						},
						new AddColumnOperation
						{
							Name = "SSN",
							Table = "People",
							ClrType = typeof(string),
							ColumnType = "char(11)",
							IsNullable = true,
						},
						new AddColumnOperation
						{
							Name = "DEF_O",
							Table = "People",
							ClrType = typeof(string),
							MaxLength = 20,
							DefaultValue = "test",
							IsNullable = true,
						},
						new AddColumnOperation
						{
							Name = "DEF_S",
							Table = "People",
							ClrType = typeof(string),
							MaxLength = 20,
							DefaultValueSql = "'x'",
							IsNullable = true,
						},
				},
				PrimaryKey = new AddPrimaryKeyOperation
				{
					Columns = new[] { "Id" },
				},
				UniqueConstraints =
				{
						new AddUniqueConstraintOperation
						{
							Columns = new[] { "SSN" },
						},
				},
				ForeignKeys =
				{
						new AddForeignKeyOperation
						{
							Columns = new[] { "EmployerId" },
							PrincipalTable = "Companies",
							PrincipalColumns = new[] { "Id" },
						},
				},
			};
			var expectedCreateTable = @"CREATE TABLE ""People"" (
    ""Id"" INTEGER NOT NULL,
    ""Id_Identity"" INTEGER GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    ""Id_Sequence"" INTEGER NOT NULL,
    ""EmployerId"" INTEGER,
    ""SSN"" char(11),
    ""DEF_O"" VARCHAR(20) DEFAULT _UTF8'test',
    ""DEF_S"" VARCHAR(20) DEFAULT 'x',
    PRIMARY KEY (""Id""),
    UNIQUE (""SSN""),
    FOREIGN KEY (""EmployerId"") REFERENCES ""Companies"" (""Id"") ON UPDATE NO ACTION ON DELETE NO ACTION
);";
			var batch = Generate(new[] { operation });
			Assert.AreEqual(3, batch.Count());
			Assert.AreEqual(NewLineEnd(expectedCreateTable), batch[0].CommandText);
			StringAssert.Contains("rdb$generator_name = ", batch[1].CommandText);
			StringAssert.StartsWith("CREATE TRIGGER ", batch[2].CommandText);
		}

		[Test]
		public void DropTable()
		{
			var operation = new DropTableOperation()
			{
				Name = "People",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"DROP TABLE ""People"";"), batch[0].CommandText);
		}

		[Test]
		public void AddColumn()
		{
			var operation = new AddColumnOperation()
			{
				Table = "People",
				Name = "NewColumn",
				ClrType = typeof(decimal),
				Schema = "schema",
				IsNullable = false,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""schema"".""People"" ADD ""NewColumn"" DECIMAL(18,2) NOT NULL;"), batch[0].CommandText);
		}

		[Test]
		public void DropColumn()
		{
			var operation = new DropColumnOperation()
			{
				Table = "People",
				Name = "DropMe",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" DROP ""DropMe"";"), batch[0].CommandText);
		}

		[Test]
		public void AlterColumnLength()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(string),
				IsNullable = true,
				MaxLength = 200,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(string),
					IsNullable = true,
					MaxLength = 100,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(2, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE VARCHAR(200);"), batch[1].CommandText);
		}

		[Test]
		public void AlterColumnNullableToNotNull()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(string),
				IsNullable = false,
				MaxLength = 100,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(string),
					IsNullable = true,
					MaxLength = 100,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(2, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" DROP NOT NULL;"), batch[0].CommandText);
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE VARCHAR(100) NOT NULL;"), batch[1].CommandText);
		}

		[Test]
		public void AlterColumnNotNullToNullable()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(string),
				IsNullable = true,
				MaxLength = 100,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(string),
					IsNullable = false,
					MaxLength = 100,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(2, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" DROP NOT NULL;"), batch[0].CommandText);
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE VARCHAR(100);"), batch[1].CommandText);
		}

		[Test]
		public void AlterColumnType()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(long),
				IsNullable = false,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(int),
					IsNullable = false,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(2, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE BIGINT NOT NULL;"), batch[1].CommandText);
		}

		[Test]
		public void AlterColumnDefault()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(int),
				DefaultValue = 20,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(int),
					DefaultValue = 10,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(4, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE INTEGER NOT NULL;"), batch[1].CommandText);
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" DROP DEFAULT;"), batch[2].CommandText);
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" SET DEFAULT 20;"), batch[3].CommandText);
		}

		[Test]
		public void AlterColumnAddIdentityColumn()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(int),
				[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.IdentityColumn,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(int),
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(2, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE INTEGER GENERATED BY DEFAULT AS IDENTITY NOT NULL;"), batch[1].CommandText);
		}

		[Test]
		public void AlterColumnAddSequenceTrigger()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(int),
				[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.SequenceTrigger,
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(int),
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(4, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE INTEGER NOT NULL;"), batch[1].CommandText);
			StringAssert.Contains("rdb$generator_name = ", batch[2].CommandText);
			StringAssert.StartsWith("CREATE TRIGGER ", batch[3].CommandText);
		}

		[Test]
		public void AlterColumnRemoveSequenceTrigger()
		{
			var operation = new AlterColumnOperation()
			{
				Table = "People",
				Name = "Col",
				ClrType = typeof(int),
				OldColumn = new ColumnOperation()
				{
					ClrType = typeof(int),
					[FbAnnotationNames.ValueGenerationStrategy] = FbValueGenerationStrategy.SequenceTrigger,
				},
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(3, batch.Count());
			StringAssert.Contains("drop trigger", batch[0].CommandText);
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""Col"" TYPE INTEGER NOT NULL;"), batch[2].CommandText);
		}

		[Test]
		public void RenameColumn()
		{
			var operation = new RenameColumnOperation()
			{
				Table = "People",
				Name = "OldCol",
				NewName = "NewCol",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ALTER COLUMN ""OldCol"" TO ""NewCol"";"), batch[0].CommandText);
		}

		[Test]
		public void CreateIndexOneColumn()
		{
			var operation = new CreateIndexOperation()
			{
				Table = "People",
				Name = "MyIndex",
				Columns = new[] { "Foo" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"CREATE INDEX ""MyIndex"" ON ""People"" (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void CreateIndexThreeColumn()
		{
			var operation = new CreateIndexOperation()
			{
				Table = "People",
				Name = "MyIndex",
				Columns = new[] { "Foo", "Bar", "Baz" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"CREATE INDEX ""MyIndex"" ON ""People"" (""Foo"", ""Bar"", ""Baz"");"), batch[0].CommandText);
		}

		[Test]
		public void CreateIndexUnique()
		{
			var operation = new CreateIndexOperation()
			{
				Table = "People",
				Name = "MyIndex",
				Columns = new[] { "Foo" },
				IsUnique = true,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"CREATE UNIQUE INDEX ""MyIndex"" ON ""People"" (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void CreateIndexFilter()
		{
			var operation = new CreateIndexOperation()
			{
				Table = "People",
				Name = "MyIndex",
				Filter = "xxx",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"CREATE INDEX ""MyIndex"" ON ""People"" COMPUTED BY (xxx);"), batch[0].CommandText);
		}

		[Test]
		public void CreateSequence()
		{
			var operation = new CreateSequenceOperation()
			{
				Name = "MySequence",
				StartValue = 34,
				IncrementBy = 56,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"CREATE SEQUENCE ""MySequence"" START WITH 34 INCREMENT BY 56;"), batch[0].CommandText);
		}

		[Test]
		public void AlterSequence()
		{
			var operation = new AlterSequenceOperation()
			{
				Name = "MySequence",
				IncrementBy = 12,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER SEQUENCE ""MySequence"" RESTART INCREMENT BY 12;"), batch[0].CommandText);
		}

		[Test]
		public void RestartSequence()
		{
			var operation = new RestartSequenceOperation()
			{
				Name = "MySequence",
				StartValue = 23,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER SEQUENCE ""MySequence"" START WITH 23;"), batch[0].CommandText);
		}

		[Test]
		public void DropSequence()
		{
			var operation = new DropSequenceOperation()
			{
				Name = "MySequence",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"DROP SEQUENCE ""MySequence"";"), batch[0].CommandText);
		}

		[Test]
		public void AddPrimaryKey()
		{
			var operation = new AddPrimaryKeyOperation()
			{
				Table = "People",
				Name = "PK_People",
				Columns = new[] { "Foo" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""PK_People"" PRIMARY KEY (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void AddPrimaryKeyNoName()
		{
			var operation = new AddPrimaryKeyOperation()
			{
				Table = "People",
				Columns = new[] { "Foo" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD PRIMARY KEY (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void DropPrimaryKey()
		{
			var operation = new DropPrimaryKeyOperation()
			{
				Table = "People",
				Name = "PK_People",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" DROP CONSTRAINT ""PK_People"";"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKey()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"");"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyNoName()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"");"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyDeleteCascade()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Cascade,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON DELETE CASCADE;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyDeleteNoAction()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.NoAction,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON DELETE NO ACTION;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyDeleteRestrict()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"");"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyDeleteSetDefault()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.SetDefault,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON DELETE SET DEFAULT;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyDeleteSetNull()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.SetNull,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON DELETE SET NULL;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyUpdateCascade()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.Cascade,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON UPDATE CASCADE;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyUpdateNoAction()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.NoAction,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON UPDATE NO ACTION;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyUpdateRestrict()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.Restrict,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"");"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyUpdateSetDefault()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.SetDefault,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON UPDATE SET DEFAULT;"), batch[0].CommandText);
		}

		[Test]
		public void AddForeignKeyUpdateSetNull()
		{
			var operation = new AddForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
				Columns = new[] { "Foo" },
				PrincipalTable = "Principal",
				PrincipalColumns = new[] { "Bar" },
				OnDelete = ReferentialAction.Restrict,
				OnUpdate = ReferentialAction.SetNull,
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""FK_People_Principal"" FOREIGN KEY (""Foo"") REFERENCES ""Principal"" (""Bar"") ON UPDATE SET NULL;"), batch[0].CommandText);
		}

		[Test]
		public void DropForeignKey()
		{
			var operation = new DropForeignKeyOperation()
			{
				Table = "People",
				Name = "FK_People_Principal",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" DROP CONSTRAINT ""FK_People_Principal"";"), batch[0].CommandText);
		}

		[Test]
		public void AddUniqueConstraintOneColumn()
		{
			var operation = new AddUniqueConstraintOperation()
			{
				Table = "People",
				Name = "UNQ_People_Foo",
				Columns = new[] { "Foo" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""UNQ_People_Foo"" UNIQUE (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void AddUniqueConstraintTwoColumns()
		{
			var operation = new AddUniqueConstraintOperation()
			{
				Table = "People",
				Name = "UNQ_People_Foo_Bar",
				Columns = new[] { "Foo", "Bar" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD CONSTRAINT ""UNQ_People_Foo_Bar"" UNIQUE (""Foo"", ""Bar"");"), batch[0].CommandText);
		}

		[Test]
		public void AddUniqueConstraintNoName()
		{
			var operation = new AddUniqueConstraintOperation()
			{
				Table = "People",
				Columns = new[] { "Foo" },
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" ADD UNIQUE (""Foo"");"), batch[0].CommandText);
		}

		[Test]
		public void DropUniqueConstraint()
		{
			var operation = new DropUniqueConstraintOperation()
			{
				Table = "People",
				Name = "UNQ_People_Foo",
			};
			var batch = Generate(new[] { operation });
			Assert.AreEqual(1, batch.Count());
			Assert.AreEqual(NewLineEnd(@"ALTER TABLE ""People"" DROP CONSTRAINT ""UNQ_People_Foo"";"), batch[0].CommandText);
		}

		IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations)
		{
			using (var db = GetDbContext<FbTestDbContext>())
			{
				var generator = db.GetService<IMigrationsSqlGenerator>();
				return generator.Generate(operations, db.Model);
			}
		}

		static string NewLineEnd(string s) => s + Environment.NewLine;
	}
#pragma warning restore EF1001
}
