-- O20-G deterministic, data-free catalog inventory for one marked disposable PostgreSQL database.
-- Metadata and ACLs only; no business rows or credential material are selected.
WITH principals(logical_identity, database_role) AS (
    VALUES
        ('runtime_app', 'mdgsm_runtime'),
        ('migration_runner', 'migration_runner'),
        ('backup_operator', 'backup_operator'),
        ('reporting_ro', 'reporting_ro'),
        ('emergency_operator', 'emergency_operator')
),
effective_privileges AS (
    SELECT p.logical_identity, p.database_role, 'database'::text AS object_type,
           '<current_disposable_database>'::text AS object_identity, v.privilege
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN LATERAL (VALUES
        ('CONNECT', has_database_privilege(r.rolname, current_database(), 'CONNECT')),
        ('CREATE',  has_database_privilege(r.rolname, current_database(), 'CREATE')),
        ('TEMP',    has_database_privilege(r.rolname, current_database(), 'TEMP'))
    ) v(privilege, allowed)
    WHERE v.allowed
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'schema', n.nspname, v.privilege
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_namespace n
    CROSS JOIN LATERAL (VALUES
        ('USAGE',  has_schema_privilege(r.rolname, n.oid, 'USAGE')),
        ('CREATE', has_schema_privilege(r.rolname, n.oid, 'CREATE'))
    ) v(privilege, allowed)
    WHERE v.allowed AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'relation,
           format('%I.%I', n.nspname, c.relname), v.privilege
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    CROSS JOIN LATERAL (VALUES
        ('SELECT',     has_table_privilege(r.rolname, c.oid, 'SELECT')),
        ('INSERT',     has_table_privilege(r.rolname, c.oid, 'INSERT')),
        ('UPDATE',     has_table_privilege(r.rolname, c.oid, 'UPDATE')),
        ('DELETE',     has_table_privilege(r.rolname, c.oid, 'DELETE')),
        ('TRUNCATE',   has_table_privilege(r.rolname, c.oid, 'TRUNCATE')),
        ('REFERENCES', has_table_privilege(r.rolname, c.oid, 'REFERENCES')),
        ('TRIGGER',    has_table_privilege(r.rolname, c.oid, 'TRIGGER'))
    ) v(privilege, allowed)
    WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f') AND v.allowed
      AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'sequence,
           format('%I.%I', n.nspname, c.relname), v.privilege
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    CROSS JOIN LATERAL (VALUES
        ('USAGE',  has_sequence_privilege(r.rolname, c.oid, 'USAGE')),
        ('SELECT', has_sequence_privilege(r.rolname, c.oid, 'SELECT')),
        ('UPDATE', has_sequence_privilege(r.rolname, c.oid, 'UPDATE'))
    ) v(privilege, allowed)
    WHERE c.relkind = 'S' AND v.allowed
      AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'column,
           format('%I.%I.%I', n.nspname, c.relname, a.attname), v.privilege
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_attribute a
    JOIN pg_class c ON c.oid = a.attrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    CROSS JOIN LATERAL (VALUES
        ('SELECT',     has_column_privilege(r.rolname, c.oid, a.attnum, 'SELECT')),
        ('INSERT',     has_column_privilege(r.rolname, c.oid, a.attnum, 'INSERT')),
        ('UPDATE',     has_column_privilege(r.rolname, c.oid, a.attnum, 'UPDATE')),
        ('REFERENCES', has_column_privilege(r.rolname, c.oid, a.attnum, 'REFERENCES'))
    ) v(privilege, allowed)
    WHERE a.attnum > 0 AND NOT a.attisdropped
      AND c.relkind IN ('r', 'p', 'v', 'f') AND v.allowed
      AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'routine,
           format('%I.%I(%s)', n.nspname, pr.proname, pg_get_function_identity_arguments(pr.oid)),
           'EXECUTE'
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_proc pr
    JOIN pg_namespace n ON n.oid = pr.pronamespace
    WHERE has_function_privilege(r.rolname, pr.oid, 'EXECUTE')
      AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
    UNION ALL
    SELECT p.logical_identity, p.database_role, 'type,
           format('%I.%I', n.nspname, t.typname), 'USAGE'
    FROM principals p
    JOIN pg_roles r ON r.rolname = p.database_role
    CROSS JOIN pg_type t
    JOIN pg_namespace n ON n.oid = t.typnamespace
    WHERE has_type_privilege(r.rolname, t.oid, 'USAGE')
      AND n.nspname <> 'information_schema' AND left(n.nspname, 3) <> 'pg_'
)
SELECT jsonb_build_object(
    'format', 'MDGSM-O20-G-CATALOG-v1',
    'server_version_num', current_setting('server_version_num')::integer,
    'database', (
        SELECT jsonb_build_object(
            'name', '<current_disposable_database>',
            'owner', pg_get_userbyid(d.datdba),
            'encoding', pg_encoding_to_char(d.encoding),
            'allow_connections', d.datallowconn,
            'is_template', d.datistemplate,
            'connection_limit', d.datconnlimit,
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(d.datacl, acldefault('d', d.datdba))) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        )
        FROM pg_database d WHERE d.datname = current_database()
    ),
    'role_bindings', (
        SELECT jsonb_agg(jsonb_build_object(
            'logical_identity', p.logical_identity,
            'database_role', p.database_role,
            'present', (r.oid IS NOT NULL),
            'login', r.rolcanlogin,
            'superuser', r.rolsuper,
            'createdb', r.rolcreatedb,
            'createrole', r.rolcreaterole,
            'replication', r.rolreplication,
            'bypassrls', r.rolbypassrls,
            'inherit', r.rolinherit
        ) ORDER BY p.logical_identity)
        FROM principals p LEFT JOIN pg_roles r ON r.rolname = p.database_role
    ),
    'roles', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', r.rolname,
            'login', r.rolcanlogin,
            'superuser', r.rolsuper,
            'createdb', r.rolcreatedb,
            'createrole', r.rolcreaterole,
            'replication', r.rolreplication,
            'bypassrls', r.rolbypassrls,
            'inherit', r.rolinherit,
            'connection_limit', r.rolconnlimit,
            'valid_until_utc', CASE WHEN r.rolvaliduntil IS NULL THEN NULL
                ELSE to_char(r.rolvaliduntil AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END,
            'config', COALESCE(to_jsonb(r.rolconfig), '[]'::jsonb)
        ) ORDER BY r.rolname), '[]'::jsonb)
        FROM pg_roles r
    ),
    'memberships', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'granted_role', granted.rolname,
            'member_role', member.rolname,
            'grantor', grantor.rolname,
            'admin_option', m.admin_option,
            'inherit_option', m.inherit_option,
            'set_option', m.set_option
        ) ORDER BY granted.rolname, member.rolname, grantor.rolname), '[]'::jsonb)
        FROM pg_auth_members m
        JOIN pg_roles granted ON granted.oid = m.roleid
        JOIN pg_roles member ON member.oid = m.member
        JOIN pg_roles grantor ON grantor.oid = m.grantor
    ),
    'schemas', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', n.nspname,
            'owner', pg_get_userbyid(n.nspowner),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(n.nspacl, acldefault('n', n.nspowner))) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY n.nspname), '[]'::jsonb)
        FROM pg_namespace n
    ),
    'relations', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'schema', n.nspname,
            'name', c.relname,
            'kind', c.relkind,
            'owner', pg_get_userbyid(c.relowner),
            'persistence', c.relpersistence,
            'is_partition', c.relispartition,
            'row_security', c.relrowsecurity,
            'force_row_security', c.relforcerowsecurity,
            'options', COALESCE(to_jsonb(c.reloptions), '[]'::jsonb),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(c.relacl,
                    CASE WHEN c.relkind = 'S' THEN acldefault('S', c.relowner)
                         ELSE acldefault('r', c.relowner) END)) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY n.nspname, c.relname, c.relkind), '[]'::jsonb)
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    ),
    'column_acls', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'schema', n.nspname,
            'relation', c.relname,
            'column', a.attname,
            'grantee', CASE WHEN x.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(x.grantee) END,
            'grantor', pg_get_userbyid(x.grantor),
            'privilege', x.privilege_type,
            'grantable', x.is_grantable
        ) ORDER BY n.nspname, c.relname, a.attnum, x.grantee, x.privilege_type), '[]'::jsonb)
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        CROSS JOIN LATERAL unnest(COALESCE(a.attacl, '{}'::aclitem[])) AS acl_entry(item)
        CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) x
        WHERE a.attnum > 0 AND NOT a.attisdropped
    ),
    'routines', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'schema', n.nspname,
            'name', pr.proname,
            'identity_arguments', pg_get_function_identity_arguments(pr.oid),
            'kind', pr.prokind,
            'owner', pg_get_userbyid(pr.proowner),
            'security_definer', pr.prosecdef,
            'leakproof', pr.proleakproof,
            'config', COALESCE(to_jsonb(pr.proconfig), '[]'::jsonb),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(pr.proacl, acldefault('f', pr.proowner))) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY n.nspname, pr.proname, pg_get_function_identity_arguments(pr.oid)), '[]'::jsonb)
        FROM pg_proc pr JOIN pg_namespace n ON n.oid = pr.pronamespace
    ),
    'types', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'schema', n.nspname,
            'name', t.typname,
            'kind', t.typtype,
            'owner', pg_get_userbyid(t.typowner),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(t.typacl, acldefault('T', t.typowner))) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY n.nspname, t.typname), '[]'::jsonb)
        FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
    ),
    'default_acls', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'creator_role', pg_get_userbyid(d.defaclrole),
            'schema', CASE WHEN d.defaclnamespace = 0 THEN NULL ELSE
                (SELECT nspname FROM pg_namespace WHERE oid = d.defaclnamespace) END,
            'object_type', d.defaclobjtype,
            'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
            'grantor', pg_get_userbyid(a.grantor),
            'privilege', a.privilege_type,
            'grantable', a.is_grantable
        ) ORDER BY d.defaclrole, d.defaclnamespace, d.defaclobjtype, a.grantee, a.privilege_type), '[]'::jsonb)
        FROM pg_default_acl d
        CROSS JOIN LATERAL unnest(COALESCE(d.defaclacl, '{}'::aclitem[])) AS acl_entry(item)
        CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
    ),
    'extensions', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', e.extname,
            'version', e.extversion,
            'schema', n.nspname,
            'owner', pg_get_userbyid(e.extowner),
            'relocatable', e.extrelocatable,
            'configuration_table_count', COALESCE(cardinality(e.extconfig), 0),
            'extension_member_object_count', (
                SELECT count(*) FROM pg_depend dep
                WHERE dep.refclassid = 'pg_extension'::regclass
                  AND dep.refobjid = e.oid AND dep.deptype = 'e'
            )
        ) ORDER BY e.extname), '[]'::jsonb)
        FROM pg_extension e JOIN pg_namespace n ON n.oid = e.extnamespace
    ),
    'languages', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', l.lanname,
            'owner', pg_get_userbyid(l.lanowner),
            'trusted', l.lanpltrusted,
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(l.lanacl, '{}'::aclitem[])) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY l.lanname), '[]'::jsonb)
        FROM pg_language l
    ),
    'foreign_data_wrappers', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', f.fdwname,
            'owner', pg_get_userbyid(f.fdwowner),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(f.fdwacl, '{}'::aclitem[])) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY f.fdwname), '[]'::jsonb)
        FROM pg_foreign_data_wrapper f
    ),
    'foreign_servers', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', s.srvname,
            'owner', pg_get_userbyid(s.srvowner),
            'wrapper', f.fdwname,
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(s.srvacl, '{}'::aclitem[])) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY s.srvname), '[]'::jsonb)
        FROM pg_foreign_server s JOIN pg_foreign_data_wrapper f ON f.oid = s.srvfdw
    ),
    'large_objects', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'oid', l.oid,
            'owner', pg_get_userbyid(l.lomowner),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(l.lomacl, '{}'::aclitem[])) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY l.oid), '[]'::jsonb)
        FROM pg_largeobject_metadata l
    ),
    'tablespaces', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'name', t.spcname,
            'owner', pg_get_userbyid(t.spcowner),
            'acl', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'grantor', pg_get_userbyid(a.grantor),
                    'grantee', CASE WHEN a.grantee = 0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
                    'privilege', a.privilege_type,
                    'grantable', a.is_grantable
                ) ORDER BY a.grantee, a.privilege_type, a.grantor)
                FROM unnest(COALESCE(t.spcacl, '{}'::aclitem[])) AS acl_entry(item)
                CROSS JOIN LATERAL aclexplode(ARRAY[acl_entry.item]) a
            ), '[]'::jsonb)
        ) ORDER BY t.spcname), '[]'::jsonb)
        FROM pg_tablespace t
    ),
    'object_ownership', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'owner', r.rolname,
            'class', d.classid::regclass::text,
            'object', pg_describe_object(d.classid, d.objid, d.objsubid),
            'dependency_type', d.deptype
        ) ORDER BY r.rolname, d.classid::regclass::text, pg_describe_object(d.classid, d.objid, d.objsubid)), '[]'::jsonb)
        FROM pg_shdepend d
        JOIN pg_roles r ON r.oid = d.refobjid
        WHERE d.refclassid = 'pg_authid'::regclass
          AND d.deptype = 'o'
          AND (
              d.dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
              OR (d.dbid = 0 AND (
                  (d.classid = 'pg_database'::regclass
                   AND d.objid = (SELECT oid FROM pg_database WHERE datname = current_database()))
                  OR d.classid = 'pg_tablespace'::regclass
              ))
          )
    ),
    'effective_privileges', (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'logical_identity', e.logical_identity,
            'database_role', e.database_role,
            'object_type', e.object_type,
            'object', e.object_identity,
            'privilege', e.privilege
        ) ORDER BY e.logical_identity, e.object_type, e.object_identity, e.privilege), '[]'::jsonb)
        FROM effective_privileges e
    )
)::text;
