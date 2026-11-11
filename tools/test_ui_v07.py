from playwright.sync_api import sync_playwright
from pathlib import Path
import subprocess, time, socket

base=Path(__file__).resolve().parents[1]
html=(base/'docs/UI_v07_interaktiv.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox'])
    page=browser.new_page(viewport={'width':420,'height':900},device_scale_factor=1)
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.set_content(html,wait_until="load")
    assert '132 alapgyakorlat' in page.locator('#count').inner_text(),page.locator('#count').inner_text()
    page.screenshot(path=str(base/'docs/v07_gyakorlattar.png'),full_page=False)
    page.locator('#newBtn').click()
    page.locator('#fName').fill('Teszt vállkörzés')
    page.locator('#fCat').select_option('mobilizálás')
    page.locator('#fRegions').fill('váll, vállöv')
    page.locator('#fPhase').select_option('warmup')
    page.locator('#fReps').fill('4 előre; 4 hátra')
    page.locator('#fSummary').fill('Lassú vállkörzés előre és hátra, kényelmes tartományban.')
    page.locator('#activateSave').click()
    assert '133 találat' in page.locator('#count').inner_text(),page.locator('#count').inner_text()
    page.locator('#q').fill('Teszt vállkörzés')
    assert page.locator('.exercise-card').count()==1
    page.locator('[data-act="edit"]').click()
    page.screenshot(path=str(base/'docs/v07_szerkeszto.png'),full_page=False)
    page.locator('#openPose').click()
    assert page.locator('#poseSvg .joint').count()==16
    page.evaluate("document.querySelector('#toast').style.display='none'")
    page.screenshot(path=str(base/'docs/v07_pozszerkeszto.png'),full_page=True)
    page.locator('#arrowMode').check()
    page.locator('#poseSvg').click(position={'x':100,'y':80})
    page.locator('#poseSvg').click(position={'x':140,'y':110})
    page.locator('#savePose').click()
    assert '2 mozgásfázis' in page.locator('#figDescription').inner_text()
    page.locator('#activateSave').click()
    page.locator('[data-act="disable"]').click()
    assert 'Kikapcsolt' in page.locator('.exercise-card').inner_text()
    page.locator('#q').fill('')
    page.locator('.nav[data-tab="exchange"]').click()
    with page.expect_download() as dl:
        page.locator('#exportBtn').click()
    download=dl.value
    assert download.suggested_filename=='mozdulj-konyvtar-v07.json'
    assert not errors,errors
    print('PASS UI prototype: list 132, create, validate, edit, pose, arrows, disable, export, no page errors')
    browser.close()

