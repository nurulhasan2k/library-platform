"""End-to-end walk through the 12 demo items of the client brief (section 9) against a running server.
Run:  python3 e2e/demo.py   (server on http://localhost:5080 with freshly seeded data)"""
import re, sys, json, os, urllib.request
from playwright.sync_api import sync_playwright

BASE = os.environ.get("BASE", "http://localhost:5080")
SHOTS = os.environ.get("SHOTS", "/home/claude/shots")
KEY = "demo-api-key-2026"
results = []

def check(name, cond, detail=""):
    results.append((name, bool(cond), detail))
    print(("  PASS  " if cond else "  FAIL  ") + name + (("   -> " + detail) if (detail and not cond) else ""))

def text(page): return page.inner_text("body")

def staff_login(page, user, pw):
    page.goto(BASE + "/staff/login")
    page.fill("#Username", user); page.fill("#Password", pw)
    page.click("button[type=submit]"); page.wait_for_load_state()

def choose_branch(page, code_text, desk=None):
    page.goto(BASE + "/staff/branch")
    page.select_option("#BranchId", label=re.compile(code_text) if False else None) if False else None
    options = page.eval_on_selector_all("#BranchId option", "els => els.map(e => [e.value, e.textContent])")
    val = [v for v, t in options if code_text in t][0]
    page.select_option("#BranchId", val)
    page.click("form[action='/staff/branch'] button[type=submit]"); page.wait_for_load_state()

def staff_logout(page):
    page.click("form[action='/staff/logout'] button"); page.wait_for_load_state()

with sync_playwright() as p:
    b = p.chromium.launch(args=["--no-sandbox"])
    ctx = b.new_context(viewport={"width": 1280, "height": 800})
    page = ctx.new_page()
    page.on("pageerror", lambda e: print("  JS ERROR:", e))

    print("\n[1] Fresh install: public home and staff login")
    page.goto(BASE + "/"); check("home page loads with results", "item" in text(page) and page.locator("article.result").count() == 12)
    page.screenshot(path=f"{SHOTS}/01-public-search.png")
    page.fill("input[name=q]", "dune"); page.click(".hero button[type=submit]"); page.wait_for_load_state()
    check("search finds Dune", "Dune" in text(page) and page.locator("article.result").count() == 1)
    page.goto(BASE + "/?q=atlas"); check("destroyed item never appears in public search", "old atlas" not in text(page))

    print("\n[2] Selecting a branch")
    staff_login(page, "reception", "reception123")
    check("reception with two branches is asked to choose", "/staff/branch" in page.url, page.url)
    page.screenshot(path=f"{SHOTS}/02-choose-branch.png")
    choose_branch(page, "CENTRAL"); check("branch chosen shows in the top bar", "Central Library" in page.inner_text("header") and "/staff/branch" not in page.url, page.url)
    staff_logout(page)
    staff_login(page, "reception.river", "reception123")
    check("reception.river has one branch and skips the choice", "/staff/branch" not in page.url and "Riverside Library" in text(page), page.url)
    staff_logout(page)

    print("\n[3] Kiosk checkout and account summary")
    k = ctx.new_page(); k.set_viewport_size({"width": 1024, "height": 768})
    k.goto(BASE + "/kiosk"); k.screenshot(path=f"{SHOTS}/03-kiosk-welcome.png")
    k.fill("#CardNumber", "LIB-1001"); k.fill("#Pin", "1111"); k.click("button.btn[type=submit]"); k.wait_for_load_state()
    check("Ava signs in at the kiosk and sees her account", "Hello, Ava Thompson" in text(k))
    k.screenshot(path=f"{SHOTS}/04-kiosk-account.png")
    k.goto(BASE + "/kiosk/checkout")
    for code in ["BK-001", "BK-002"]:
        k.fill("#code", code); k.press("#code", "Enter"); k.wait_for_load_state()
    check("two items checked out at the kiosk", "BK-001" in text(k) and "BK-002" in text(k) and "Checked out this visit (2)" in text(k))
    k.screenshot(path=f"{SHOTS}/05-kiosk-checkout.png")
    k.fill("#code", "TY-001"); k.press("#code", "Enter"); k.wait_for_load_state()
    check("a destroyed item is refused with a message", "destroyed" in text(k))
    k.click("a.btn-ok"); k.wait_for_load_state(); check("receipt shows both items", "BK-001" in text(k) and "due" in text(k))
    k.screenshot(path=f"{SHOTS}/06-kiosk-receipt.png")
    k.click("form[action='/kiosk/signout'] button"); k.wait_for_load_state()
    check("signing out returns to the welcome screen", "Welcome" in text(k))
    k.goto(BASE + "/kiosk/home"); check("a signed-out kiosk can't see an account", "Welcome" in text(k))
    # Emma and Daniel summaries
    for card, pin, expect in [("LIB-1005", "5555", "Items on loan (5)"), ("LIB-1004", "4444", "Fines:")]:
        k.goto(BASE + "/kiosk"); k.fill("#CardNumber", card); k.fill("#Pin", pin); k.click("button.btn[type=submit]"); k.wait_for_load_state()
        check(f"account summary for {card}", expect in text(k), text(k)[:200])
        if card == "LIB-1004": k.screenshot(path=f"{SHOTS}/07-kiosk-fines.png")
        k.click("form[action='/kiosk/signout'] button"); k.wait_for_load_state()
    # lockout
    for i in range(5):
        k.goto(BASE + "/kiosk"); k.fill("#CardNumber", "LIB-1010"); k.fill("#Pin", "9999"); k.click("button.btn[type=submit]"); k.wait_for_load_state()
    check("five wrong PINs lock the card", "locked" in text(k).lower(), text(k)[:300])

    print("\n[4] Public API")
    def api(path, key=KEY, method="GET"):
        req = urllib.request.Request(BASE + path, headers={"X-Api-Key": key} if key else {}, method=method)
        try:
            with urllib.request.urlopen(req) as r: return r.status, json.loads(r.read() or b"null")
        except urllib.error.HTTPError as e:
            body = e.read(); return e.code, (json.loads(body) if body else None)
    s, d = api("/api/v1/items?pageSize=100"); check("API returns items from three branches", s == 200 and len({i["branch"]["code"] for i in d["items"]}) == 3)
    check("API never returns the destroyed atlas", all(i["code"] != "BK-040" for i in d["items"]))
    s, d = api("/api/v1/categories"); check("API lists 9 categories", s == 200 and len(d) == 9)
    s, d = api("/api/v1/branches"); check("API lists 3 branches with hours", s == 200 and len(d) == 3 and len(d[0]["openingHours"]) == 7)
    s, d = api("/api/v1/status"); check("API status endpoint", s == 200 and d["status"] == "ok")
    check("API without key is 401", api("/api/v1/items", key=None)[0] == 401)
    check("API with wrong key is 403", api("/api/v1/items", key="x")[0] == 403)
    check("API POST is 405", api("/api/v1/items", method="POST")[0] == 405)
    check("API DELETE is 405", api("/api/v1/items/BK-001", method="DELETE")[0] == 405)
    page.goto(BASE + "/api/docs"); page.wait_for_selector("details.endpoint"); check("API docs page lists endpoints", page.locator("details.endpoint").count() >= 5)
    page.screenshot(path=f"{SHOTS}/08-api-docs.png", full_page=False)

    print("\n[5] Borrowing and notification logging (desk and kiosk)")
    staff_login(page, "reception", "reception123"); choose_branch(page, "CENTRAL")
    page.goto(BASE + "/desk"); page.screenshot(path=f"{SHOTS}/09-desk.png")
    page.fill("#borrower", "LIB-1001"); page.fill("#code", "MU-001"); page.click("form[action='/desk/borrow'] button"); page.wait_for_load_state()
    check("desk lends MU-001 to Ava", "Lent" in text(page) and "Rumours" in text(page), text(page)[:300])
    page.goto(BASE + "/notifications?q=Rumours"); check("a Borrowed notification appears for MU-001", "You borrowed" in text(page) and "Rumours" in text(page))
    page.screenshot(path=f"{SHOTS}/10-notifications.png")

    print("\n[7] Multi-branch inventory and transfer")
    page.goto(BASE + "/")
    opts = page.eval_on_selector_all("select[name=branch] option", "els => els.map(e => [e.value, e.textContent])")
    north = [v for v, t in opts if "Northside" in t][0]
    page.goto(BASE + "/?branch=" + north); n_north = page.locator("article.result").count()
    check("search can be limited to one branch", 0 < n_north <= 12 and all("Northside" in c for c in page.eval_on_selector_all("article.result", "els => els.map(e => e.innerText)")))
    page.goto(BASE + "/transfers"); page.screenshot(path=f"{SHOTS}/11-transfers.png")
    page.fill("#code", "BK-032"); page.select_option("#to", label="Northside Library"); page.click("form[action='/transfers/request'] button"); page.wait_for_load_state()
    check("transfer requested", "Transfer requested" in text(page))
    page.click("tr:has-text('BK-032') button:has-text('Dispatch')"); page.wait_for_load_state(); check("transfer dispatched (in transit)", "now in transit" in text(page))
    page.goto(BASE + "/desk"); page.fill("#borrower", "LIB-1001"); page.fill("#code", "MU-012"); page.click("form[action='/desk/borrow'] button"); page.wait_for_load_state()
    check("MU-012 (in transit) can't be borrowed", "in transit" in text(page) and "Lent" not in text(page), text(page)[:300])
    page.goto(BASE + "/desk"); page.fill("#borrower", "LIB-1001"); page.fill("#code", "BK-030"); page.click("form[action='/desk/borrow'] button"); page.wait_for_load_state()
    check("an item with an active transfer can't be borrowed", "in transit" in text(page), text(page)[:300])
    choose_branch(page, "NORTH")
    page.goto(BASE + "/transfers"); page.click("tr:has-text('BK-032') button:has-text('Receive')"); page.wait_for_load_state()
    check("North receives the transfer", "received" in text(page).lower() and "Northside" in text(page))
    page.goto(BASE + "/items/BK-032"); check("item page shows its new branch", "Northside Library" in text(page))
    page.goto(BASE + "/desk"); page.fill("#borrower", "LIB-1001"); page.fill("#code", "BK-032"); page.click("form[action='/desk/borrow'] button"); page.wait_for_load_state()
    check("BK-032 can now be lent at North", "Lent" in text(page), text(page)[:300])
    staff_logout(page)

    print("\n[8] Reservation and next-person notification")
    staff_login(page, "reception", "reception123"); choose_branch(page, "CENTRAL")
    page.goto(BASE + "/holds"); page.screenshot(path=f"{SHOTS}/12-holds.png"); check("BK-010 has three people waiting", text(page).count("Waiting") >= 3)
    page.goto(BASE + "/desk"); page.fill("#rcode", "BK-010"); page.click("form[action='/desk/return'] button"); page.wait_for_load_state()
    check("Emma's copy of BK-010 is returned", "Returned" in text(page))
    page.goto(BASE + "/holds"); check("Grace is now Ready", "Ready" in text(page) and "Grace Liu" in text(page))
    page.goto(BASE + "/notifications?q=Grace"); check("Grace was notified", "is ready for you" in text(page))
    page.goto(BASE + "/desk"); page.fill("#borrower", "LIB-1008"); page.fill("#code", "BK-010"); page.click("form[action='/desk/borrow'] button"); page.wait_for_load_state()
    check("Hugo can't borrow the held item", "held for another patron" in text(page), text(page)[:300])
    staff_logout(page)
    staff_login(page, "admin", "admin123"); choose_branch(page, "CENTRAL")
    page.goto(BASE + "/admin/items?q=MU-010")
    page.select_option("tr:has-text('MU-010') select[name=status]", "Available"); page.wait_for_load_state()
    check("admin repairs MU-010", "Status set to available" in text(page), text(page)[:300])
    page.goto(BASE + "/notifications?q=Kind of Blue"); check("Grace notified that MU-010 is ready", "Kind of Blue" in text(page) and "ready" in text(page))

    print("\n[6] Due-date and fine notification simulation")
    page.goto(BASE + "/notifications"); page.screenshot(path=f"{SHOTS}/13-notifications-admin.png")
    page.click("form[action='/notifications/run'] button"); page.wait_for_load_state()
    check("running the checks creates today's notifications", "Checks run" in text(page))
    page.goto(BASE + "/notifications?q=Farid"); check("Farid has a due-soon notice", "due" in text(page).lower())
    page.goto(BASE + "/notifications?q=Daniel"); check("Daniel has fine-accruing notices", "overdue" in text(page))
    page.goto(BASE + "/notifications")
    page.click("form[action='/notifications/advance']:has(input[value='7']) button"); page.wait_for_load_state()
    page.click("form[action='/notifications/run'] button"); page.wait_for_load_state()
    check("moving the date forward and running again creates more", "new notification" in text(page), text(page)[:300])
    page.goto(BASE + "/notifications?type=FineAccruing"); page.screenshot(path=f"{SHOTS}/14-fine-notices.png")
    page.click("form[action='/notifications/date'] button[value=clear]"); page.wait_for_load_state(); check("real date restored", "real date" in text(page))

    print("\n[9] CSV and external import")
    page.goto(BASE + "/admin/import"); page.screenshot(path=f"{SHOTS}/15-import.png")
    csv = "type,code,name,description,branch,category,author,artist,year,age\nbook,BK-701,Sample Import One,Test,RIVER,History,Some Author,,,\nbook,BK-701,Duplicate code,,,History,A,,,\nmusic,MU-701,Bad year,,,Rock,,Band,abc,\ntoy,TY-701,Marble set,,,Building,,,,5\n"
    open("/tmp/smoke/import.csv", "w").write(csv)
    page.set_input_files("#file", "/tmp/smoke/import.csv"); page.select_option("#branchId", label="Riverside Library"); page.click("form[action='/admin/import/upload'] button"); page.wait_for_load_state()
    check("preview shows 2 ready and 2 skipped rows", "ready to import" in text(page) and page.locator("tr.row-bad").count() == 2, text(page)[:400])
    page.screenshot(path=f"{SHOTS}/16-import-preview.png")
    page.click("form[action='/admin/import/commit'] button"); page.wait_for_load_state(); check("valid rows imported", "Imported 2 of 4" in text(page), text(page)[:300])
    page.goto(BASE + "/?q=Sample+Import"); check("imported item appears in public search", "Sample Import One" in text(page) and "Riverside" in text(page))
    page.goto(BASE + "/admin/import"); page.select_option("#pbranch", label="Riverside Library"); page.click("form[action='/admin/import/provider'] button"); page.wait_for_load_state()
    check("external provider preview: 10 ready, 2 skipped", "ready to import" in text(page) and page.locator("tr.row-bad").count() == 2)
    page.click("form[action='/admin/import/commit'] button"); page.wait_for_load_state(); check("external rows imported", "Imported 10 of 12" in text(page))

    print("\n[10][11] Manager analytics and exports")
    staff_logout(page); staff_login(page, "manager", "manager123")
    check("manager lands on the dashboard without a branch choice", "Dashboard" in text(page))
    page.goto(BASE + "/reports"); page.screenshot(path=f"{SHOTS}/17-manager-dashboard.png", full_page=True); check("dashboard shows charts", page.locator(".bar-fill").count() > 5)
    for name in ["borrowing", "fines", "inventory"]:
        page.goto(BASE + f"/reports/{name}"); check(f"{name} report loads", page.locator("table").count() >= 2)
        if name == "borrowing": page.screenshot(path=f"{SHOTS}/18-report-borrowing.png", full_page=True)
        for fmt, sig in [("csv", b"\xef\xbb\xbf"), ("pdf", b"%PDF")]:
            resp = ctx.request.get(BASE + f"/reports/{name}?format={fmt}")
            body = resp.body(); check(f"{name} {fmt} export downloads", resp.ok and body.startswith(sig) and len(body) > 500, f"{resp.status} {len(body)}")
            if fmt == "pdf": open(f"/tmp/smoke/{name}.pdf", "wb").write(body)
            if fmt == "csv": open(f"/tmp/smoke/{name}.csv", "wb").write(body)
    page.goto(BASE + "/admin/items"); check("manager is refused admin pages", "access" in text(page).lower() or "/denied" in page.url, page.url)
    staff_logout(page)

    print("\n[12] Signed-out visitors are kept out of staff pages (database reset is covered by scripts/reset-db and the seed tests)")
    page.goto(BASE + "/desk"); check("anonymous visitors are sent to sign in", "/staff/login" in page.url)

    print("\n[mobile] responsive public portal")
    m = b.new_context(viewport={"width": 390, "height": 844}, device_scale_factor=2, is_mobile=True).new_page()
    m.goto(BASE + "/"); m.screenshot(path=f"{SHOTS}/19-mobile-search.png")
    overflow = m.evaluate("document.documentElement.scrollWidth > document.documentElement.clientWidth")
    check("no sideways scrolling on a phone", not overflow)
    m.goto(BASE + "/items/BK-010"); m.screenshot(path=f"{SHOTS}/20-mobile-item.png")
    m.goto(BASE + "/branches"); m.screenshot(path=f"{SHOTS}/21-mobile-branches.png"); check("no sideways scrolling on branches", not m.evaluate("document.documentElement.scrollWidth > document.documentElement.clientWidth"))
    b.close()

fails = [r for r in results if not r[1]]
print(f"\n{len(results) - len(fails)} passed, {len(fails)} failed")
sys.exit(1 if fails else 0)
