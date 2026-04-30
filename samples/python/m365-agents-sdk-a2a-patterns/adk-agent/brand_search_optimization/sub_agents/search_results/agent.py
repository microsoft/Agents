# Copyright 2025 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import time
import warnings
import re

import selenium
from google.adk.agents.llm_agent import Agent
from google.adk.tools.load_artifacts_tool import load_artifacts_tool
from google.adk.tools.tool_context import ToolContext
from google.genai import types
from PIL import Image
from selenium.webdriver.common.by import By

try:
    from bs4 import BeautifulSoup  # type: ignore
    BS4_AVAILABLE = True
except ImportError:
    BS4_AVAILABLE = False
    BeautifulSoup = None  # type: ignore
    print("⚠️ Warning: BeautifulSoup not installed. Product extraction will fail. Run: pip install beautifulsoup4")

from ...shared_libraries import constants
from . import prompt

warnings.filterwarnings("ignore", category=UserWarning)

# Lazy initialization - driver will be created only when needed
driver = None

def _ensure_driver():
    """Lazy initialization of Selenium WebDriver - only when actually needed."""
    global driver
    if driver is not None:
        return driver
    
    if constants.DISABLE_WEB_DRIVER:
        return None
    
    import os
    # Detect if running on Cloud Run (check for K_SERVICE env var)
    is_cloud_run = os.getenv('K_SERVICE') is not None

    if is_cloud_run:
        # Use Chrome on Cloud Run (standard in Cloud Run containers)
        print("🌩️ Detected Cloud Run environment - using Chrome")
        from selenium.webdriver.chrome.service import Service as ChromeService
        from selenium.webdriver.chrome.options import Options as ChromeOptions

        chrome_options = ChromeOptions()
        chrome_options.add_argument("--headless=new")
        chrome_options.add_argument("--no-sandbox")
        chrome_options.add_argument("--disable-dev-shm-usage")
        chrome_options.add_argument("--disable-gpu")
        chrome_options.add_argument("--window-size=1920,1080")
        chrome_options.add_argument("--remote-debugging-port=9222")
        chrome_options.add_argument("--disable-extensions")
        chrome_options.add_argument("--disable-software-rasterizer")

        # Selenium Manager handles ChromeDriver automatically
        driver = selenium.webdriver.Chrome(options=chrome_options)
    else:
        # Use Firefox locally (better ARM64 Windows support)
        print("💻 Detected local environment - using Firefox")
        from selenium.webdriver.firefox.service import Service as FirefoxService
        from selenium.webdriver.firefox.options import Options as FirefoxOptions
        from webdriver_manager.firefox import GeckoDriverManager

        firefox_options = FirefoxOptions()
        firefox_options.add_argument("--headless")
        firefox_options.add_argument("--width=1920")
        firefox_options.add_argument("--height=1080")

        # Use GeckoDriver with automatic management
        driver = selenium.webdriver.Firefox(
            service=FirefoxService(GeckoDriverManager().install()),
            options=firefox_options
        )
    
    return driver


def go_to_url(url: str) -> str:
    """Navigates the browser to the given URL."""
    if constants.DISABLE_WEB_DRIVER:
        return f"Web driver is disabled. Cannot navigate to {url}. Enable DISABLE_WEB_DRIVER=0 in .env to use real Google Shopping scraping."
    
    driver = _ensure_driver()
    if driver is None:
        return "Web driver initialization failed"
    
    print(f"🌐 Navigating to URL: {url}")
    driver.get(url.strip())
    return f"Navigated to URL: {url}"


async def take_screenshot(tool_context: ToolContext) -> dict:
    """Takes a screenshot and saves it with the given filename. called 'load artifacts' after to load the image"""
    if constants.DISABLE_WEB_DRIVER:
        return {"error": "Web driver disabled. Cannot take screenshot. Set DISABLE_WEB_DRIVER=0 in .env"}
    
    driver = _ensure_driver()
    if driver is None:
        return {"error": "Web driver initialization failed"}
    
    timestamp = time.strftime("%Y%m%d-%H%M%S")
    filename = f"screenshot_{timestamp}.png"
    print(f"📸 Taking screenshot and saving as: {filename}")
    driver.save_screenshot(filename)

    image = Image.open(filename)

    await tool_context.save_artifact(
        filename,
        types.Part.from_bytes(data=image.tobytes(), mime_type="image/png"),
    )

    return {"status": "ok", "filename": filename}


def click_at_coordinates(x: int, y: int) -> str:
    """Clicks at the specified coordinates on the screen."""
    driver.execute_script(f"window.scrollTo({x}, {y});")
    driver.find_element(By.TAG_NAME, "body").click()


def find_element_with_text(text: str) -> str:
    """Finds an element on the page with the given text."""
    print(f"🔍 Finding element with text: '{text}'")  # Added print statement

    try:
        element = driver.find_element(By.XPATH, f"//*[text()='{text}']")
        if element:
            return "Element found."
        else:
            return "Element not found."
    except selenium.common.exceptions.NoSuchElementException:
        return "Element not found."
    except selenium.common.exceptions.ElementNotInteractableException:
        return "Element not interactable, cannot click."


def click_element_with_text(text: str) -> str:
    """Clicks on an element on the page with the given text."""
    print(f"🖱️ Clicking element with text: '{text}'")  # Added print statement

    try:
        element = driver.find_element(By.XPATH, f"//*[text()='{text}']")
        element.click()
        return f"Clicked element with text: {text}"
    except selenium.common.exceptions.NoSuchElementException:
        return "Element not found, cannot click."
    except selenium.common.exceptions.ElementNotInteractableException:
        return "Element not interactable, cannot click."
    except selenium.common.exceptions.ElementClickInterceptedException:
        return "Element click intercepted, cannot click."


def enter_text_into_element(text_to_enter: str, element_id: str) -> str:
    """Enters text into an element with the given ID."""
    print(
        f"📝 Entering text '{text_to_enter}' into element with ID: {element_id}"
    )  # Added print statement

    try:
        input_element = driver.find_element(By.ID, element_id)
        input_element.send_keys(text_to_enter)
        return (
            f"Entered text '{text_to_enter}' into element with ID: {element_id}"
        )
    except selenium.common.exceptions.NoSuchElementException:
        return "Element with given ID not found."
    except selenium.common.exceptions.ElementNotInteractableException:
        return "Element not interactable, cannot click."


def scroll_down_screen() -> str:
    """Scrolls down the screen by a moderate amount."""
    print("⬇️ scroll the screen")  # Added print statement
    driver.execute_script("window.scrollBy(0, 500)")
    return "Scrolled down the screen."


def get_page_source() -> str:
    LIMIT = 1000000
    """Returns the current page source."""
    print("📄 Getting page source...")  # Added print statement
    return driver.page_source[0:LIMIT]


def analyze_webpage_and_determine_action(
    page_source: str, user_task: str, tool_context: ToolContext
) -> str:
    """Analyzes the webpage and determines the next action (scroll, click, etc.)."""
    print(
        "🤔 Analyzing webpage and determining next action..."
    )  # Added print statement

    analysis_prompt = f"""
    You are an expert web page analyzer.
    You have been tasked with controlling a web browser to achieve a user's goal.
    The user's task is: {user_task}
    Here is the current HTML source code of the webpage:
    ```html
    {page_source}
    ```

    Based on the webpage content and the user's task, determine the next best action to take.
    Consider actions like: completing page source, scrolling down to see more content, clicking on links or buttons to navigate, or entering text into input fields.

    Think step-by-step:
    1. Briefly analyze the user's task and the webpage content.
    2. If source code appears to be incomplete, complete it to make it valid html. Keep the product titles as is. Only complete missing html syntax
    3. Identify potential interactive elements on the page (links, buttons, input fields, etc.).
    4. Determine if scrolling is necessary to reveal more content.
    5. Decide on the most logical next action to progress towards completing the user's task.

    Your response should be a concise action plan, choosing from these options:
    - "COMPLETE_PAGE_SOURCE": If source code appears to be incomplete, complte it to make it valid html
    - "SCROLL_DOWN": If more content needs to be loaded by scrolling.
    - "CLICK: <element_text>": If a specific element with text <element_text> should be clicked. Replace <element_text> with the actual text of the element.
    - "ENTER_TEXT: <element_id>, <text_to_enter>": If text needs to be entered into an input field. Replace <element_id> with the ID of the input element and <text_to_enter> with the text to enter.
    - "TASK_COMPLETED": If you believe the user's task is likely completed on this page.
    - "STUCK": If you are unsure what to do next or cannot progress further.
    - "ASK_USER": If you need clarification from the user on what to do next.

    If you choose "CLICK" or "ENTER_TEXT", ensure the element text or ID is clearly identifiable from the webpage source. If multiple similar elements exist, choose the most relevant one based on the user's task.
    If you are unsure, or if none of the above actions seem appropriate, default to "ASK_USER".

    Example Responses:
    - SCROLL_DOWN
    - CLICK: Learn more
    - ENTER_TEXT: search_box_id, Gemini API
    - TASK_COMPLETED
    - STUCK
    - ASK_USER

    What is your action plan?
    """
    return analysis_prompt


def _extract_amazon_products(keyword: str) -> list:
    """
    Scrape Amazon for competing products. More stable than Google Shopping.
    
    Args:
        keyword: Search term (e.g., "Nike Active")
    
    Returns:
        List of product dicts with 'title' and 'price' keys, or empty list on failure
    """
    try:
        driver = _ensure_driver()
        if driver is None:
            return []
        
        print("🛒 Trying Amazon...")
        url = f"https://www.amazon.com/s?k={keyword.replace(' ', '+')}"
        driver.get(url)
        time.sleep(2)  # Wait for page load
        
        soup = BeautifulSoup(driver.page_source, 'html.parser')
        products = []
        
        # Amazon product containers
        items = soup.select('div[data-component-type="s-search-result"]')
        
        for item in items[:10]:
            try:
                # Title: h2 > a > span
                title_elem = item.select_one('h2.s-line-clamp-2 span')
                # Price: span.a-price > span.a-offscreen
                price_elem = item.select_one('span.a-price span.a-offscreen')
                
                if title_elem:
                    title = title_elem.get_text(strip=True)
                    price = price_elem.get_text(strip=True) if price_elem else "N/A"
                    
                    if title and len(title) > 10:
                        products.append({"title": title, "price": price})
                        if len(products) >= 3:
                            break
            except:
                continue
        
        if products:
            print(f"✅ Amazon: Extracted {len(products)} products")
        return products
        
    except Exception as e:
        print(f"⚠️ Amazon failed: {str(e)}")
        return []


def _extract_google_shopping_products_internal(keyword: str) -> list:
    """
    Scrape Google Shopping as fallback.
    
    Args:
        keyword: Search term
    
    Returns:
        List of product dicts or empty list
    """
    try:
        driver = _ensure_driver()
        if driver is None:
            return []
        
        print("🔍 Trying Google Shopping...")
        url = f"https://www.google.com/search?tbm=shop&q={keyword.replace(' ', '+')}"
        driver.get(url)
        time.sleep(2)
        
        soup = BeautifulSoup(driver.page_source, 'html.parser')
        products = []
        
        # Multiple selector patterns for Google Shopping
        selectors = [
            {'container': '.sh-dgr__grid-result', 'title': '.tAxDx', 'price': '.a8Pemb'},
            {'container': '.sh-dlr__list-result', 'title': '.Xjkr3b', 'price': '.a8Pemb'},
            {'container': '.sh-np__click-target', 'title': 'h3', 'price': 'span[aria-label*="dollar"]'},
            {'container': '[data-sh-np]', 'title': '.tAxDx, .Xjkr3b', 'price': '.a8Pemb'},
        ]
        
        for selector_set in selectors:
            containers = soup.select(selector_set['container'])
            
            for container in containers[:10]:
                try:
                    title_elem = container.select_one(selector_set['title'])
                    price_elem = container.select_one(selector_set['price'])
                    
                    if title_elem:
                        title = title_elem.get_text(strip=True)
                        price = price_elem.get_text(strip=True) if price_elem else "N/A"
                        
                        # Clean price
                        price_match = re.search(r'\$[\d,]+\.?\d*', price)
                        if price_match:
                            price = price_match.group(0)
                        
                        if title and len(title) > 5:
                            products.append({"title": title, "price": price})
                            if len(products) >= 3:
                                break
                except:
                    continue
            
            if len(products) >= 3:
                break
        
        if products:
            print(f"✅ Google Shopping: Extracted {len(products)} products")
        return products
        
    except Exception as e:
        print(f"⚠️ Google Shopping failed: {str(e)}")
        return []


def extract_google_shopping_products(keyword: str = None) -> str:
    """
    Multi-source product extraction with intelligent fallback:
    1. SerpAPI (if configured) - production quality, no bot detection
    2. Amazon web scraping (fallback)
    3. Google Shopping web scraping (last resort)
    
    Uses structured API data when available, falls back to HTML parsing.
    
    Args:
        keyword: Search term (e.g., "moisture wicking socks"). If not provided, extracts from current URL.

    Returns:
        Formatted string with extracted competitor products or error message
    """
    if constants.DISABLE_WEB_DRIVER:
        return "Error: Web driver is disabled. Set DISABLE_WEB_DRIVER=0 in .env to enable real product extraction."
    
    if not BS4_AVAILABLE:
        return "Error: BeautifulSoup not installed. Run: pip install beautifulsoup4"

    try:
        # If keyword not provided, try to extract from current URL
        if not keyword:
            driver = _ensure_driver()
            if driver:
                current_url = driver.current_url
                # Handle both Amazon (k=) and Google Shopping (q=) URL parameters
                keyword_match = re.search(r'[?&](?:q|k)=([^&]+)', current_url)
                keyword = keyword_match.group(1).replace('+', ' ') if keyword_match else "product"
            else:
                keyword = "product"
        
        # DO NOT extract brand from keyword - the keyword is already brand-free from keyword_finding_agent
        # Brand filtering should be done by root agent passing exclude_brand explicitly
        brand = None  # Don't filter by brand from keyword
        
        print(f"🔍 Extracting competitor products for: {keyword}")
        
        # Try SerpAPI first (most reliable, no bot detection)
        try:
            from ...tools.serp_connector import get_competitor_products
            
            competitors = get_competitor_products(keyword, exclude_brand=brand)
            
            # Check if it's an error response
            if isinstance(competitors, dict) and "error" in competitors:
                print(f"⚠️ SerpAPI unavailable: {competitors['message']}")
                print("🌐 Falling back to web scraping...")
            elif competitors:
                # Format SerpAPI results - show all 10 products
                result = f"### Competitor Search Results\n\nTop {len(competitors)} competing products for \"{keyword}\":\n\n"
                for i, product in enumerate(competitors, 1):
                    result += f"{i}. {product['title']} - {product['price']}\n"
                result += f"\n*Source: SerpAPI (Google Shopping)*"
                return result
        except ImportError:
            print("⚠️ SerpAPI library not installed (pip install google-search-results)")
            print("🌐 Using web scraping fallback...")
        except Exception as e:
            print(f"⚠️ SerpAPI error: {str(e)}")
            print("🌐 Using web scraping fallback...")
        
        # Fallback to web scraping (may hit bot detection)
        products = []
        
        # Try Amazon scraping
        products = _extract_amazon_products(keyword)
        
        # Fallback to Google Shopping scraping
        if not products:
            products = _extract_google_shopping_products_internal(keyword)
        
        # Save debug HTML from last attempt
        import os
        debug_file = os.path.join(os.getcwd(), "competitor_search_debug.html")
        with open(debug_file, "w", encoding="utf-8") as f:
            f.write(driver.page_source)
        print(f"💾 Saved debug HTML to: {debug_file}")
        
        # Format results
        if products:
            result = "### Competitor Search Results\n\nTop competing products:\n\n"
            for i, product in enumerate(products[:3], 1):
                result += f"{i}. {product['title']} - {product['price']}\n"
            print(f"✅ Successfully extracted {len(products[:3])} competitor products")
            return result
        else:
            print("❌ All extraction sources failed")
            return "Error: Could not extract competitor products from Amazon or Google Shopping. Check debug HTML file for details."


    except Exception as e:
        print(f"❌ Error extracting products: {str(e)}")
        return f"Error extracting products: {str(e)}. Try taking a screenshot to see what the page looks like."


search_results_agent = Agent(
    model=constants.MODEL,
    name="search_results_agent",
    description="Get top 10 search results info for a keyword using web browsing and HTML parsing",
    instruction=prompt.SEARCH_RESULT_AGENT_PROMPT,
    tools=[
        go_to_url,
        take_screenshot,
        find_element_with_text,
        click_element_with_text,
        enter_text_into_element,
        scroll_down_screen,
        get_page_source,
        load_artifacts_tool,
        analyze_webpage_and_determine_action,
        extract_google_shopping_products,
    ],
)
