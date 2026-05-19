## ⚙️ ENGINEERING ROOM EXECUTION ORDER (CRITICAL FIX)

**Execution Sequence Must Be Strictly Followed:**

1. **Backend Agent (BOOTSTRAP FIRST)**
   - Generate project skeleton
   - Create core API structure (.NET solution, Controllers, Services)
   - Define and scaffold the initial database schema (MSSQL tables, EF Core DbContext)

2. **Database Definition**
   - Must be completed **before** any Frontend work begins
   - Produces the initial `schema.sql` and EF Core model classes

3. **Frontend Agent**
   - Build UI shell (React entry point, routing, global state store)
   - Integrate API calls into implemented endpoints
   - Connect UI components to the newly created backend endpoints

4. **QA Agent**
   - Test only the features that have been implemented
   - Cannot design new infrastructure or add novel features
   - Validate correctness of each API response and frontend interaction

5. **DevOps Agent**
   - Can activate **only after** architecture + code are in place
   - Define CI/CD pipeline (build, test, deploy)
   - Generate Dockerfiles/K8s manifests based on the real system

### 🚫 Role Scope Limitation Rule
- Agents may **not** invent unrelated tools (e.g., Matlab, random IDEs)
- Scope is limited to the phase of the system lifecycle assigned to them
- No jumping ahead in the pipeline – every agent works only on its designated phase

*This order ensures deterministic, conflict‑free progression from idea to deployment.* 